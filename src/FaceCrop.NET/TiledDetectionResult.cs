namespace FaceCrop;

/// <summary>
/// Faces found by <see cref="TiledFaceDetector"/>.
/// </summary>
/// <param name="Faces">Confirmed faces, highest score first.</param>
/// <param name="DetectionPass">
/// <c>full</c> when only the full image was scanned; otherwise a summary such as
/// <c>full_and_tiles; rejected_unconfirmed=1; raw_detections=12</c>.
/// </param>
/// <param name="RawDetectionCount">Detections from all windows before merging.</param>
/// <param name="RejectedCount">Merged faces dropped for lacking enough confirming windows.</param>
public sealed record TiledDetectionResult(IReadOnlyList<FaceCandidate> Faces, string DetectionPass, int RawDetectionCount, int RejectedCount);
