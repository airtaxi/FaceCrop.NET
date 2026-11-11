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
    /// <param name="length">Available length on the axis.</param>
    /// <param name="side">Square side.</param>
    /// <param name="lower">Start of the interval that must stay inside.</param>
    /// <param name="upper">End of the interval that must stay inside.</param>
    /// <param name="center">Preferred center of the square.</param>
    /// <exception cref="FaceRegionDoesNotFitException">The interval is longer than the square or cannot be placed inside the axis.</exception>
    public static int AxisOrigin(int length, int side, double lower, double upper, double center)
    {
        var lowest = Math.Max(0, (int)Math.Ceiling(upper - side));
        var highest = Math.Min(length - side, (int)Math.Floor(lower));
        if (lowest > highest) throw new FaceRegionDoesNotFitException(string.Create(CultureInfo.InvariantCulture, $"face_region_does_not_fit: axis_length={length}, square_side={side}, face_interval=({lower:F3}, {upper:F3}), allowed_origin=({lowest}, {highest})"));
        return Math.Max(lowest, Math.Min(highest, (int)Math.Round(center - side / 2.0)));
    }

    /// <summary>
    /// Returns the largest square inside a <paramref name="width"/> by <paramref name="height"/> area that contains the given bounds and is closest to the given center.
    /// </summary>
    /// <exception cref="FaceRegionDoesNotFitException">The bounds cannot fit inside the square.</exception>
    public static PixelRegion CropBox(int width, int height, double left, double top, double right, double bottom, double centerX, double centerY)
    {
        var side = Math.Min(width, height);
        var x = AxisOrigin(width, side, left, right, centerX);
        var y = AxisOrigin(height, side, top, bottom, centerY);
        return new PixelRegion(x, y, x + side, y + side);
    }

    /// <summary>
    /// Plans a crop that contains every selected face box and keypoint. When the faces overlap the excluded right panel,
    /// the face region is clipped at its boundary and, if needed, the crop falls back to the eyes, nose and mouth.
    /// </summary>
    /// <param name="selected">Faces that must be inside the crop.</param>
    /// <param name="width">Source image width.</param>
    /// <param name="height">Source image height.</param>
    /// <param name="usableWidth">Width left of the excluded right panel.</param>
    public static FaceCropPlan PlanFaceCrop(IReadOnlyList<FaceCandidate> selected, int width, int height, int usableWidth)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Count == 0) throw new ArgumentException("At least one face is required.", nameof(selected));
        double scaleWidth = width;
        double scaleHeight = height;
        var detail = "";
        var xs = new List<double>();
        var ys = new List<double>();
        var points = new List<(double X, double Y)>();
        foreach (var face in selected)
        {
            var box = face.Box;
            xs.AddRange([box.OriginX / scaleWidth * width, (box.OriginX + box.Width) / scaleWidth * width]);
            ys.AddRange([box.OriginY / scaleHeight * height, (box.OriginY + box.Height) / scaleHeight * height]);
            foreach (var keypoint in face.Keypoints)
            {
                xs.Add(keypoint.X * width);
                ys.Add(keypoint.Y * height);
            }
            points.AddRange(face.Keypoints.Take(CenterKeypointCount).Select(keypoint => (keypoint.X * width, keypoint.Y * height)));
        }
        var left = Math.Max(0, xs.Min());
        var top = Math.Max(0, ys.Min());
        var right = Math.Min(width, xs.Max());
        var bottom = Math.Min(height, ys.Max());
        var (centerX, centerY) = points.Count > 0 ? ((points.Min(point => point.X) + points.Max(point => point.X)) / 2, (points.Min(point => point.Y) + points.Max(point => point.Y)) / 2) : ((left + right) / 2, (top + bottom) / 2);
        var status = FaceCropStatus.FaceCrop;
        var overlapsStrip = right > usableWidth;
        if (overlapsStrip)
        {
            if (left >= usableWidth) throw new FaceCropException("selected_faces_entirely_inside_excluded_strip");
            right = usableWidth;
            centerX = Math.Min(centerX, usableWidth);
            status = FaceCropStatus.FaceCropStripClipped;
            detail += "; Face region clipped at right strip boundary.";
        }
        PixelRegion cropBox;
        try { cropBox = CropBox(usableWidth, height, left, top, right, bottom, centerX, centerY); }
        catch (FaceRegionDoesNotFitException) when (overlapsStrip)
        {
            // Close-ups that run into the panel keep the eyes, nose and mouth when the whole face box is too tall.
            var visible = points.Where(point => 0 <= point.X && point.X <= usableWidth && 0 <= point.Y && point.Y <= height).ToList();
            var (featureLeft, featureTop, featureRight, featureBottom) = (centerX, centerY, centerX, centerY);
            if (visible.Count > 0)
            {
                (featureLeft, featureTop, featureRight, featureBottom) = (visible.Min(point => point.X), visible.Min(point => point.Y), visible.Max(point => point.X), visible.Max(point => point.Y));
                (centerX, centerY) = ((featureLeft + featureRight) / 2, (featureTop + featureBottom) / 2);
            }
            try { cropBox = CropBox(usableWidth, height, featureLeft, featureTop, featureRight, featureBottom, centerX, centerY); }
            catch (FaceRegionDoesNotFitException) { cropBox = CropBox(usableWidth, height, centerX, centerY, centerX, centerY, centerX, centerY); }
            detail += " Full face region did not fit; used feature-centered partial crop.";
        }
        return new FaceCropPlan(cropBox, status, detail);
    }

    /// <summary>
    /// Drops low-confidence faces, lowest priority first, until the remaining faces fit in one square crop.
    /// Recovery requires an anchor face scoring at least 0.90; only faces scoring below 0.85 and at least 0.10 below the anchor can be removed.
    /// Faces well below an upper anchor face are removed before others.
    /// </summary>
    /// <param name="selected">Faces that did not fit together.</param>
    /// <param name="width">Source image width.</param>
    /// <param name="height">Source image height.</param>
    /// <param name="usableWidth">Width left of the excluded right panel.</param>
    /// <param name="originalError">The message of the failure that triggered recovery.</param>
    /// <exception cref="FaceCropException">No anchor face qualifies, or no removal makes the faces fit.</exception>
    public static LowConfidenceRecovery RecoverLowConfidence(IReadOnlyList<FaceCandidate> selected, int width, int height, int usableWidth, string originalError)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Count == 0) throw new ArgumentException("At least one face is required.", nameof(selected));
        var best = selected.MaxBy(face => face.Score)!;
        var bestScore = best.Score;
        var bestCenterY = GetCenterY(best);
        if (bestScore < MinimumAnchorScore) throw new FaceCropException(originalError + "; recovery_skipped: highest_score_below_0.90");
        bool IsLowerPriority(FaceCandidate face) => bestCenterY <= height * UpperRegionRatio && GetCenterY(face) - bestCenterY >= height * LowerOffsetRatio;
        var candidates = selected.Where(face => face.Score < MaximumRemovableScore && bestScore - face.Score >= MinimumScoreGap).OrderBy(face => !IsLowerPriority(face)).ThenBy(face => face.Score).ToList();
        var remaining = selected.ToList();
        var removed = new List<RemovedCandidate>();
        foreach (var face in candidates)
        {
            remaining.RemoveAll(other => ReferenceEquals(other, face));
            var centerY = GetCenterY(face);
            removed.Add(new RemovedCandidate(face.Score, bestScore - face.Score, face.Box, face.SupportWindows, IsLowerPriority(face), centerY <= height * UpperRegionRatio || centerY < bestCenterY));
            FaceCropPlan plan;
            try { plan = PlanFaceCrop(remaining, width, height, usableWidth); }
            catch (FaceRegionDoesNotFitException) { continue; }
            return new LowConfidenceRecovery(plan.Box, originalError, selected.Count, remaining.Count, removed, removed.Any(candidate => candidate.UpperCandidateRemoved), plan.Status, plan.Detail);
        }
        throw new FaceCropException(originalError + "; recovery_failed; removed_candidates=" + ReportJson.Serialize(LowConfidenceRecovery.ToJsonValue(removed), escapeNonAscii: true));
    }

    private static double GetCenterY(FaceCandidate face) => face.Box.OriginY + face.Box.Height / 2;
}
