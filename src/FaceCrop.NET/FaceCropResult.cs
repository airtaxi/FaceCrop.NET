using FaceCrop.Imaging;
using FaceCrop.Internal;

namespace FaceCrop;

/// <summary>
/// The outcome of analyzing or cropping one image. When <see cref="Status"/> is <see cref="FaceCropStatus.Error"/>,
/// the other properties hold whatever was computed before the failure.
/// </summary>
public sealed record FaceCropResult
{
    /// <summary>
    /// How the image was handled.
    /// </summary>
    public required FaceCropStatus Status { get; init; }

    /// <summary>
    /// The square crop region in source pixels, or <see langword="null"/> when no crop was planned.
    /// </summary>
    public PixelRegion? CropBox { get; init; }

    /// <summary>
    /// Diagnostic text: the detection pass and crop notes, the recovery report as JSON, or the error message.
    /// </summary>
    public string Detail { get; init; } = "";

    /// <summary>
    /// Faces found in the image.
    /// </summary>
    public IReadOnlyList<FaceCandidate> Faces { get; init; } = [];

    /// <summary>
    /// Width of the source image after orientation, or 0 if it was not decoded.
    /// </summary>
    public int ImageWidth { get; init; }

    /// <summary>
    /// Height of the source image after orientation, or 0 if it was not decoded.
    /// </summary>
    public int ImageHeight { get; init; }

    /// <summary>
    /// Width excluded from the right edge.
    /// </summary>
    public int ExcludedRightPixels { get; init; }

    /// <summary>
    /// The low-confidence recovery report when <see cref="Status"/> is <see cref="FaceCropStatus.FaceCropLowConfidenceFiltered"/>.
    /// </summary>
    public LowConfidenceRecovery? Recovery { get; init; }

    /// <summary>
    /// The exception that caused an <see cref="FaceCropStatus.Error"/> result.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Serializes the detected faces as JSON: box, scores, support windows and keypoints in source pixels.
    /// </summary>
    public string GetDiagnosticsJson()
    {
        var faces = Faces.Select(face => ReportJson.Object(("box", new[] { face.Box.OriginX * ImageWidth / ImageWidth, face.Box.OriginY * ImageHeight / ImageHeight, face.Box.Width * ImageWidth / ImageWidth, face.Box.Height * ImageHeight / ImageHeight }), ("scores", face.Scores), ("support_windows", face.SupportWindows), ("keypoints", face.Keypoints.Select(point => new[] { point.X * ImageWidth, point.Y * ImageHeight }).ToList())));
        return ReportJson.Serialize(faces.ToList(), escapeNonAscii: false);
    }
}
