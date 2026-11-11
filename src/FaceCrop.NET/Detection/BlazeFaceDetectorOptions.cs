namespace FaceCrop.Detection;

/// <summary>
/// Settings for <see cref="BlazeFaceDetector"/>.
/// </summary>
public sealed record BlazeFaceDetectorOptions
{
    /// <summary>
    /// Path to a BlazeFace short-range ONNX model. When <see langword="null"/>, the embedded model is used.
    /// </summary>
    public string? ModelPath { get; init; }

    /// <summary>
    /// Minimum score, in the range (0, 1], for a candidate to be kept.
    /// </summary>
    public float MinDetectionConfidence { get; init; } = 0.5f;

    /// <summary>
    /// Intersection-over-union above which overlapping candidates are merged.
    /// </summary>
    public float MinSuppressionThreshold { get; init; } = 0.3f;
}
