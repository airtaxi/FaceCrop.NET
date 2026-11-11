namespace FaceCrop.Imaging;

/// <summary>
/// WebP encoder settings.
/// </summary>
public sealed record WebpEncodeOptions
{
    /// <summary>
    /// Lossy encoder quality from 0 to 100. Lossless output always stores pixels exactly and ignores this value.
    /// </summary>
    public int Quality { get; init; } = 85;

    /// <summary>
    /// Encodes without loss when <see langword="true"/>.
    /// </summary>
    public bool Lossless { get; init; }
}
