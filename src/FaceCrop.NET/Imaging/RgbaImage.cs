namespace FaceCrop.Imaging;

/// <summary>
/// An 8-bit RGBA image stored row by row without padding.
/// </summary>
/// <param name="width">Image width in pixels.</param>
/// <param name="height">Image height in pixels.</param>
/// <param name="pixels">RGBA bytes, four per pixel, in row-major order.</param>
public sealed class RgbaImage(int width, int height, byte[] pixels)
{
    /// <summary>
    /// Number of bytes used by one pixel.
    /// </summary>
    public const int BytesPerPixel = 4;

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int Width { get; } = width > 0 ? width : throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int Height { get; } = height > 0 ? height : throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

    /// <summary>
    /// RGBA bytes, four per pixel, in row-major order.
    /// </summary>
    public byte[] Pixels { get; } = ValidatePixels(width, height, pixels);

    /// <summary>
    /// Copies a rectangular region into a new image.
    /// </summary>
    /// <param name="region">The region to copy. It must be non-empty and lie inside the image.</param>
    public RgbaImage Crop(PixelRegion region)
    {
        if (region.Left < 0 || region.Top < 0 || region.Right > Width || region.Bottom > Height || region.Width <= 0 || region.Height <= 0) throw new ArgumentOutOfRangeException(nameof(region), region, "The region must be non-empty and lie inside the image.");
        var rowLength = region.Width * BytesPerPixel;
        var result = new byte[rowLength * region.Height];
        for (var row = 0; row < region.Height; row++) Buffer.BlockCopy(Pixels, ((region.Top + row) * Width + region.Left) * BytesPerPixel, result, row * rowLength, rowLength);
        return new RgbaImage(region.Width, region.Height, result);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every pixel is fully opaque.
    /// </summary>
    public bool IsOpaque()
    {
        for (var offset = BytesPerPixel - 1; offset < Pixels.Length; offset += BytesPerPixel)
        {
            if (Pixels[offset] != byte.MaxValue)
            {
                return false;
            }
        }
        return true;
    }

    private static byte[] ValidatePixels(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.LongLength != (long)width * height * BytesPerPixel) throw new ArgumentException("The pixel buffer must contain width * height * 4 bytes.", nameof(pixels));
        return pixels;
    }
}
