using System.Globalization;
using FaceCrop.Imaging;
using FaceCrop.Internal;

namespace FaceCrop;

/// <summary>
/// Plans the largest square crop that keeps the selected faces inside and stays as close as possible to their center.
/// </summary>
public static class SquareCropPlanner
{
    private const double UpperRegionRatio = 0.45;
    private const double LowerOffsetRatio = 0.20;
    private const double MinimumAnchorScore = 0.90;
    private const double MaximumRemovableScore = 0.85;
    private const double MinimumScoreGap = 0.10;
    private const int CenterKeypointCount = 4;

    /// <summary>
    /// Returns the integer crop origin on one axis that keeps <c>[lower, upper]</c> inside the square and is closest to centering it on <paramref name="center"/>.
    /// </summary>
    /// <param name="start">Start of the available range on the axis, inclusive.</param>
    /// <param name="end">End of the available range on the axis, exclusive.</param>
    /// <param name="side">Square side.</param>
    /// <param name="lower">Start of the interval that must stay inside.</param>
    /// <param name="upper">End of the interval that must stay inside.</param>
    /// <param name="center">Preferred center of the square.</param>
    /// <exception cref="FaceRegionDoesNotFitException">The interval is longer than the square or cannot be placed inside the range.</exception>
    public static int AxisOrigin(int start, int end, int side, double lower, double upper, double center)
    {
        var lowest = Math.Max(start, (int)Math.Ceiling(upper - side));
        var highest = Math.Min(end - side, (int)Math.Floor(lower));
        if (lowest > highest) throw new FaceRegionDoesNotFitException(string.Create(CultureInfo.InvariantCulture, $"face_region_does_not_fit: axis_range=({start}, {end}), square_side={side}, face_interval=({lower:F3}, {upper:F3}), allowed_origin=({lowest}, {highest})"));
        return Math.Max(lowest, Math.Min(highest, (int)Math.Round(center - side / 2.0)));
    }

    /// <summary>
    /// Returns the largest square inside <paramref name="area"/> that contains the given bounds and is closest to the given center.
    /// </summary>
    /// <param name="area">The region the square must stay inside, in source pixels.</param>
    /// <param name="left">Left edge of the bounds that must stay inside.</param>
    /// <param name="top">Top edge of the bounds that must stay inside.</param>
    /// <param name="right">Right edge of the bounds that must stay inside.</param>
    /// <param name="bottom">Bottom edge of the bounds that must stay inside.</param>
    /// <param name="centerX">Preferred horizontal center of the square.</param>
    /// <param name="centerY">Preferred vertical center of the square.</param>
    /// <exception cref="FaceRegionDoesNotFitException">The bounds cannot fit inside the square.</exception>
    public static PixelRegion CropBox(PixelRegion area, double left, double top, double right, double bottom, double centerX, double centerY)
    {
        var side = Math.Min(area.Width, area.Height);
        var x = AxisOrigin(area.Left, area.Right, side, left, right, centerX);
        var y = AxisOrigin(area.Top, area.Bottom, side, top, bottom, centerY);
        return new PixelRegion(x, y, x + side, y + side);
    }

    /// <summary>
    /// Plans a crop inside <paramref name="area"/> that contains every selected face box and keypoint.
    /// Parts of the faces that reach outside the area are ignored.
    /// </summary>
    /// <param name="selected">Faces that must be inside the crop.</param>
    /// <param name="width">Source image width.</param>
    /// <param name="height">Source image height.</param>
    /// <param name="area">The region the crop must stay inside, in source pixels.</param>
    /// <exception cref="FaceRegionDoesNotFitException">The faces cannot fit inside one square.</exception>
    public static PixelRegion PlanFaceCrop(IReadOnlyList<FaceCandidate> selected, int width, int height, PixelRegion area)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Count == 0) throw new ArgumentException("At least one face is required.", nameof(selected));
        var horizontalPositions = new List<double>();
        var verticalPositions = new List<double>();
        var points = new List<(double X, double Y)>();
        foreach (var face in selected)
        {
            var box = face.Box;
            horizontalPositions.AddRange([box.OriginX, box.OriginX + box.Width]);
            verticalPositions.AddRange([box.OriginY, box.OriginY + box.Height]);
            foreach (var keypoint in face.Keypoints)
            {
                horizontalPositions.Add(keypoint.X * width);
                verticalPositions.Add(keypoint.Y * height);
            }
            points.AddRange(face.Keypoints.Take(CenterKeypointCount).Select(keypoint => (keypoint.X * width, keypoint.Y * height)));
        }
        var left = Math.Max(area.Left, horizontalPositions.Min());
        var top = Math.Max(area.Top, verticalPositions.Min());
        var right = Math.Min(area.Right, horizontalPositions.Max());
        var bottom = Math.Min(area.Bottom, verticalPositions.Max());
        var (centerX, centerY) = points.Count > 0 ? ((points.Min(point => point.X) + points.Max(point => point.X)) / 2, (points.Min(point => point.Y) + points.Max(point => point.Y)) / 2) : ((left + right) / 2, (top + bottom) / 2);
        return CropBox(area, left, top, right, bottom, centerX, centerY);
    }

    /// <summary>
    /// Drops low-confidence faces, lowest priority first, until the remaining faces fit in one square crop.
    /// Recovery requires an anchor face scoring at least 0.90; only faces scoring below 0.85 and at least 0.10 below the anchor can be removed.
    /// Faces well below an anchor face in the upper part of the area are removed before others.
    /// </summary>
    /// <param name="selected">Faces that did not fit together.</param>
    /// <param name="width">Source image width.</param>
    /// <param name="height">Source image height.</param>
    /// <param name="area">The region the crop must stay inside, in source pixels.</param>
    /// <param name="originalError">The message of the failure that triggered recovery.</param>
    /// <exception cref="FaceCropException">No anchor face qualifies, or no removal makes the faces fit.</exception>
    public static LowConfidenceRecovery RecoverLowConfidence(IReadOnlyList<FaceCandidate> selected, int width, int height, PixelRegion area, string originalError)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Count == 0) throw new ArgumentException("At least one face is required.", nameof(selected));
        var best = selected.MaxBy(face => face.Score)!;
        var bestScore = best.Score;
        var bestCenterY = GetCenterY(best);
        if (bestScore < MinimumAnchorScore) throw new FaceCropException(originalError + "; recovery_skipped: highest_score_below_0.90");
        var upperRegionBottom = area.Top + area.Height * UpperRegionRatio;
        bool IsLowerPriority(FaceCandidate face) => bestCenterY <= upperRegionBottom && GetCenterY(face) - bestCenterY >= area.Height * LowerOffsetRatio;
        var candidates = selected.Where(face => face.Score < MaximumRemovableScore && bestScore - face.Score >= MinimumScoreGap).OrderBy(face => !IsLowerPriority(face)).ThenBy(face => face.Score).ToList();
        var remaining = selected.ToList();
        var removed = new List<RemovedCandidate>();
        foreach (var face in candidates)
        {
            remaining.RemoveAll(other => ReferenceEquals(other, face));
            var centerY = GetCenterY(face);
            removed.Add(new RemovedCandidate(face.Score, bestScore - face.Score, face.Box, face.SupportWindows, IsLowerPriority(face), centerY <= upperRegionBottom || centerY < bestCenterY));
            PixelRegion cropBox;
            try { cropBox = PlanFaceCrop(remaining, width, height, area); }
            catch (FaceRegionDoesNotFitException) { continue; }
            return new LowConfidenceRecovery(cropBox, originalError, selected.Count, remaining.Count, removed, removed.Any(candidate => candidate.UpperCandidateRemoved));
        }
        throw new FaceCropException(originalError + "; recovery_failed; removed_candidates=" + ReportJson.Serialize(LowConfidenceRecovery.ToJsonValue(removed), escapeNonAscii: true));
    }

    private static double GetCenterY(FaceCandidate face) => face.Box.OriginY + face.Box.Height / 2;
}
