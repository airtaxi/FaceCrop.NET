namespace FaceCrop;

/// <summary>
/// Controls whether overlapping tiles are scanned in addition to the full image.
/// </summary>
public enum TileDetectionMode
{
    /// <summary>
    /// Scan tiles only when the full-image pass does not find exactly one face.
    /// </summary>
    Fallback,

    /// <summary>
    /// Always scan tiles.
    /// </summary>
    Always,

    /// <summary>
    /// Use the full-image pass only.
    /// </summary>
    Off,
}
