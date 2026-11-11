using FaceCrop.Imaging;
using FaceCrop.Internal;

namespace FaceCrop;

/// <summary>
/// The result of dropping lower-confidence faces so that the remaining faces fit in one square crop.
/// </summary>
/// <param name="Box">The square crop region found after removing faces.</param>
/// <param name="OriginalError">The error raised before recovery.</param>
/// <param name="FacesBefore">Number of selected faces before recovery.</param>
/// <param name="FacesAfter">Number of faces kept.</param>
/// <param name="RemovedCandidates">Faces removed, in removal order.</param>
/// <param name="ReviewRequired">At least one removed face was in the upper part of the image or above the anchor face.</param>
/// <param name="RecoveredCropStatus">Status of the crop planned for the remaining faces.</param>
/// <param name="RecoveredCropDetail">Detail of the crop planned for the remaining faces.</param>
public sealed record LowConfidenceRecovery(PixelRegion Box, string OriginalError, int FacesBefore, int FacesAfter, IReadOnlyList<RemovedCandidate> RemovedCandidates, bool ReviewRequired, FaceCropStatus RecoveredCropStatus, string RecoveredCropDetail)
{
    /// <summary>
    /// Serializes the recovery report as JSON.
    /// </summary>
    public string ToJson() => ReportJson.Serialize(ReportJson.Object(("original_error", OriginalError), ("faces_before", FacesBefore), ("faces_after", FacesAfter), ("removed_candidates", ToJsonValue(RemovedCandidates)), ("review_required", ReviewRequired), ("recovered_crop_status", RecoveredCropStatus.Code), ("recovered_crop_detail", RecoveredCropDetail)), escapeNonAscii: false);

    internal static object ToJsonValue(IEnumerable<RemovedCandidate> candidates) => candidates.Select(candidate => ReportJson.Object(("score", candidate.Score), ("gap_from_best", candidate.GapFromBest), ("box", new[] { candidate.Box.OriginX, candidate.Box.OriginY, candidate.Box.Width, candidate.Box.Height }), ("support_windows", candidate.SupportWindows), ("lower_position_priority", candidate.LowerPositionPriority), ("upper_candidate_removed", candidate.UpperCandidateRemoved))).ToList();
}
