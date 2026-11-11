using System.Globalization;

namespace FaceCrop.Imaging;

/// <summary>
/// A rectangle in pixel coordinates. The right and bottom edges are exclusive.
/// </summary>
/// <param name="Left">Left edge, inclusive.</param>
/// <param name="Top">Top edge, inclusive.</param>
/// <param name="Right">Right edge, exclusive.</param>
/// <param name="Bottom">Bottom edge, exclusive.</param>
public readonly record struct PixelRegion(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Region width in pixels.
    /// </summary>
    public int Width => Right - Left;

    /// <summary>
    /// Region height in pixels.
    /// </summary>
    public int Height => Bottom - Top;

    /// <summary>
    /// Formats the region as <c>(left, top, right, bottom)</c>.
    /// </summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({Left}, {Top}, {Right}, {Bottom})");
}
