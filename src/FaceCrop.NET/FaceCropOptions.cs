using FaceCrop.Imaging;

namespace FaceCrop;

/// <summary>
/// Settings for <see cref="FaceCropper"/>.
/// </summary>
public sealed record FaceCropOptions
{
    /// <summary>
    /// What to do when no face is found. Defaults to <see cref="NoFaceMode.Skip"/>.
    /// </summary>
    public NoFaceMode NoFace { get; init; } = NoFaceMode.Skip;

    /// <summary>
    /// Which faces the crop must contain. Defaults to <see cref="FaceSelectionMode.All"/>.
    /// </summary>
    public FaceSelectionMode FaceSelection { get; init; } = FaceSelectionMode.All;

    /// <summary>
    /// Whether a flat-color panel on the right edge is detected and excluded. Defaults to <see cref="RightStripMode.Auto"/>.
    /// </summary>
    public RightStripMode RightStrip { get; init; } = RightStripMode.Auto;

    /// <summary>
    /// Width in pixels to exclude from the right edge. When set, it overrides <see cref="RightStrip"/>.
    /// </summary>
    public int? RightStripPixels { get; init; }

    /// <summary>
    /// Minimum detection score in the range (0, 1]. Defaults to 0.5.
    /// </summary>
    public float MinDetectionConfidence { get; init; } = 0.5f;

    /// <summary>
    /// Each detection window is shrunk to fit inside this many pixels per side. Must be at least 128. Defaults to 1600.
    /// </summary>
    public int DetectSize { get; init; } = 1600;

    /// <summary>
    /// Whether overlapping tiles are scanned. Defaults to <see cref="TileDetectionMode.Fallback"/>.
    /// </summary>
    public TileDetectionMode TileDetection { get; init; } = TileDetectionMode.Fallback;

    /// <summary>
    /// Minimum number of distinct detection windows that must confirm a face when tiles are scanned. Defaults to 2.
    /// </summary>
    public int MinFaceVotes { get; init; } = 2;

    /// <summary>
    /// Color used to fill transparent areas of the output. When <see langword="null"/>, transparency is preserved.
    /// </summary>
    public RgbColor? Background { get; init; }

    /// <summary>
    /// WebP encoder settings used by <see cref="FaceCropper.CropFile"/>.
    /// </summary>
    public WebpEncodeOptions Webp { get; init; } = new();

    /// <summary>
    /// Path to a BlazeFace short-range ONNX model. When <see langword="null"/>, the embedded model is used.
    /// </summary>
    public string? ModelPath { get; init; }
}
