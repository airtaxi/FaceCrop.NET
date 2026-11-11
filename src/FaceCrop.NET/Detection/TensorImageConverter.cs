using FaceCrop.Imaging;

namespace FaceCrop.Detection;

/// <summary>
/// Converts an image to the 128x128 NHWC float input of the model, scaled to [-1, 1].
/// Sampling follows OpenCV's <c>warpPerspective</c> with bilinear interpolation and a zero border:
/// coordinates are computed per 64-pixel block, rounded to 1/32 pixel, and blended with fixed-point weights.
/// </summary>
internal static class TensorImageConverter
{
    internal const int InputSize = 128;
    internal const int ChannelCount = 3;
    private const int InterpolationBits = 5;
    private const int InterpolationTableSize = 1 << InterpolationBits;
    private const int InterpolationMask = InterpolationTableSize - 1;
    private const int BlockWidth = 64;
    private const int WeightBits = InterpolationBits * 2;
    private static readonly float[] s_normalizedValues = CreateNormalizedValues();

    internal static float[] Convert(RgbaImage image, LetterboxTransform letterbox)
    {
        var inverse = PerspectiveTransform.CreateInverse(letterbox, InputSize);
        var tensor = new float[InputSize * InputSize * ChannelCount];
        for (var y = 0; y < InputSize; y++)
        {
            for (var blockX = 0; blockX < InputSize; blockX += BlockWidth)
            {
                var rowX = inverse[0] * blockX + inverse[1] * y + inverse[2];
                var rowY = inverse[3] * blockX + inverse[4] * y + inverse[5];
                var rowW = inverse[6] * blockX + inverse[7] * y + inverse[8];
                for (var offsetX = 0; offsetX < BlockWidth; offsetX++)
                {
                    var w = rowW + inverse[6] * offsetX;
                    w = w != 0 ? InterpolationTableSize / w : 0;
                    var fixedX = RoundToInt((rowX + inverse[0] * offsetX) * w);
                    var fixedY = RoundToInt((rowY + inverse[3] * offsetX) * w);
                    var tensorOffset = (y * InputSize + blockX + offsetX) * ChannelCount;
                    Sample(image, SaturateToShort(fixedX >> InterpolationBits), SaturateToShort(fixedY >> InterpolationBits), fixedX & InterpolationMask, fixedY & InterpolationMask, tensor.AsSpan(tensorOffset, ChannelCount));
                }
            }
        }
        return tensor;
    }

    private static void Sample(RgbaImage image, int sourceX, int sourceY, int fractionX, int fractionY, Span<float> destination)
    {
        var topLeftWeight = (InterpolationTableSize - fractionX) * (InterpolationTableSize - fractionY);
        var topRightWeight = fractionX * (InterpolationTableSize - fractionY);
        var bottomLeftWeight = (InterpolationTableSize - fractionX) * fractionY;
        var bottomRightWeight = fractionX * fractionY;
        for (var channel = 0; channel < ChannelCount; channel++)
        {
            var sum = ReadChannel(image, sourceX, sourceY, channel) * topLeftWeight + ReadChannel(image, sourceX + 1, sourceY, channel) * topRightWeight + ReadChannel(image, sourceX, sourceY + 1, channel) * bottomLeftWeight + ReadChannel(image, sourceX + 1, sourceY + 1, channel) * bottomRightWeight;
            destination[channel] = s_normalizedValues[(sum + (1 << (WeightBits - 1))) >> WeightBits];
        }
    }

    private static int ReadChannel(RgbaImage image, int x, int y, int channel) => (uint)x < (uint)image.Width && (uint)y < (uint)image.Height ? image.Pixels[(y * image.Width + x) * RgbaImage.BytesPerPixel + channel] : 0;

    private static int RoundToInt(double value) => (int)Math.Round(Math.Clamp(value, int.MinValue, int.MaxValue), MidpointRounding.ToEven);

    private static int SaturateToShort(int value) => Math.Clamp(value, short.MinValue, short.MaxValue);

    private static float[] CreateNormalizedValues()
    {
        const float Scale = (1f - -1f) / (255f - 0f);
        const float Offset = -1f;
        var values = new float[256];
        for (var value = 0; value < values.Length; value++) values[value] = value * Scale + Offset;
        return values;
    }
}
