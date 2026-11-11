namespace FaceCrop.Imaging;

/// <summary>
/// Composites RGBA pixels over an opaque color with Pillow's integer <c>alpha_composite</c> arithmetic.
/// </summary>
internal static class AlphaCompositor
{
    private const int PrecisionBits = 7;
    private const uint BackgroundAlpha = byte.MaxValue;

    internal static RgbaImage Flatten(RgbaImage image, RgbColor background)
    {
        var source = image.Pixels;
        var result = new byte[source.Length];
        for (var offset = 0; offset < source.Length; offset += RgbaImage.BytesPerPixel)
        {
            uint alpha = source[offset + 3];
            if (alpha == 0)
            {
                result[offset] = background.Red;
                result[offset + 1] = background.Green;
                result[offset + 2] = background.Blue;
                result[offset + 3] = byte.MaxValue;
                continue;
            }
            var blend = BackgroundAlpha * (255 - alpha);
            var outputAlpha255 = alpha * 255 + blend;
            var sourceCoefficient = alpha * 255 * 255 * (1u << PrecisionBits) / outputAlpha255;
            var backgroundCoefficient = 255 * (1u << PrecisionBits) - sourceCoefficient;
            result[offset] = Blend(source[offset], background.Red, sourceCoefficient, backgroundCoefficient);
            result[offset + 1] = Blend(source[offset + 1], background.Green, sourceCoefficient, backgroundCoefficient);
            result[offset + 2] = Blend(source[offset + 2], background.Blue, sourceCoefficient, backgroundCoefficient);
            result[offset + 3] = (byte)ShiftForDivideBy255(outputAlpha255 + 0x80);
        }
        return new RgbaImage(image.Width, image.Height, result);
    }

    private static byte Blend(uint sourceValue, uint backgroundValue, uint sourceCoefficient, uint backgroundCoefficient) => (byte)(ShiftForDivideBy255(sourceValue * sourceCoefficient + backgroundValue * backgroundCoefficient + (0x80u << PrecisionBits)) >> PrecisionBits);

    private static uint ShiftForDivideBy255(uint value) => ((value >> 8) + value) >> 8;
}
