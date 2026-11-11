using ImageMagick;
using ImageMagick.Formats;

namespace FaceCrop.Imaging;

/// <summary>
/// Decodes source images and encodes WebP results.
/// </summary>
public static class ImageFile
{
    private const int WebpMethod = 6;

    /// <summary>
    /// Decodes a single-frame image file, applies its EXIF orientation and keeps its ICC profile.
    /// </summary>
    /// <param name="path">The image file to read.</param>
    /// <exception cref="FaceCropException">The file contains more than one frame.</exception>
    public static LoadedImage Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using (var frames = new MagickImageCollection())
        {
            frames.Ping(path, CreateReadSettings());
            EnsureSingleFrame(frames);
        }
        using var image = new MagickImage(path, CreateReadSettings());
        return Decode(image);
    }

    /// <summary>
    /// Decodes a single-frame encoded image, applies its EXIF orientation and keeps its ICC profile.
    /// </summary>
    /// <param name="data">The encoded image bytes.</param>
    /// <exception cref="FaceCropException">The data contains more than one frame.</exception>
    public static LoadedImage Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using (var frames = new MagickImageCollection())
        {
            frames.Ping(data, CreateReadSettings());
            EnsureSingleFrame(frames);
        }
        using var image = new MagickImage(data, CreateReadSettings());
        return Decode(image);
    }

    /// <summary>
    /// Encodes an image as WebP. The file is written to a temporary name first and then moved into place.
    /// </summary>
    /// <param name="image">The pixels to encode.</param>
    /// <param name="path">Destination path. Missing directories are created.</param>
    /// <param name="options">Encoder settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="iccProfile">An ICC profile to embed, or <see langword="null"/>.</param>
    public static void SaveWebp(RgbaImage image, string path, WebpEncodeOptions? options = null, byte[]? iccProfile = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(path);
        options ??= new WebpEncodeOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(options.Quality, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Quality, 100, nameof(options));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The destination path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.webp");
        try
        {
            using var output = new MagickImage();
            output.ReadPixels(image.Pixels, new PixelReadSettings((uint)image.Width, (uint)image.Height, StorageType.Char, PixelMapping.RGBA));
            if (image.IsOpaque()) output.Alpha(AlphaAction.Off);
            if (iccProfile is not null) output.SetProfile(new ImageProfile("icc", iccProfile));
            // ImageMagick also applies the quality as the near-lossless level, so lossless output leaves it unset to keep every pixel exact.
            if (!options.Lossless) output.Quality = (uint)options.Quality;
            output.Write(temporaryPath, new WebPWriteDefines { Lossless = options.Lossless, Method = WebpMethod });
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    // Standard sRGB ICC profiles in PNG files are dropped on read unless they are explicitly preserved.
    private static MagickReadSettings CreateReadSettings() => new(new PngReadDefines { PreserveiCCP = true });

    private static void EnsureSingleFrame(MagickImageCollection frames)
    {
        if (frames.Count != 1)
        {
            throw new FaceCropException("animated_or_multipage_image_not_supported");
        }
    }

    private static LoadedImage Decode(MagickImage image)
    {
        image.AutoOrient();
        if (image.ColorSpace == ColorSpace.CMYK) image.ColorSpace = ColorSpace.sRGB;
        var iccProfile = image.GetColorProfile()?.ToByteArray();
        using var pixelCollection = image.GetPixelsUnsafe();
        var pixels = pixelCollection.ToByteArray(PixelMapping.RGBA) ?? throw new FaceCropException("pixel_export_failed");
        return new LoadedImage(new RgbaImage((int)image.Width, (int)image.Height, pixels), iccProfile);
    }
}
