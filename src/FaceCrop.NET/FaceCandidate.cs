using FaceCrop.Detection;
using FaceCrop.Imaging;

namespace FaceCrop;

/// <summary>
/// A face located in the full source image.
/// </summary>
/// <param name="Box">Face box in source image pixels.</param>
/// <param name="Keypoints">Six keypoints normalized to the source image, in detector order.</param>
/// <param name="Scores">Detector scores for this face.</param>
/// <param name="Region">The detection window (the full image or a tile) in which the face was found.</param>
/// <param name="SupportWindows">Number of distinct detection windows that found this face.</param>
public sealed record FaceCandidate(FaceBox Box, IReadOnlyList<NormalizedPoint> Keypoints, IReadOnlyList<double> Scores, PixelRegion Region, int SupportWindows)
{
    /// <summary>
    /// The highest detector score, or 0 when there are no scores.
    /// </summary>
    public double Score => Scores.Count == 0 ? 0 : Scores.Max();
}
