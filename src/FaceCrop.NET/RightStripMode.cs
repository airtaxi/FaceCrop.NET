namespace FaceCrop;

/// <summary>
/// Controls exclusion of a flat-color panel attached to the right edge.
/// </summary>
public enum RightStripMode
{
    /// <summary>
    /// Detect the panel automatically and keep the crop to its left.
    /// </summary>
    Auto,

    /// <summary>
    /// Use the full image width.
    /// </summary>
    Off,
}
