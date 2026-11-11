namespace FaceCrop.Imaging;

/// <summary>
/// Reproduces Pillow's 8-bit RGB resampling (convolution coefficients, fixed-point accumulation,
/// <c>reduce</c> pre-shrinking and <c>thumbnail</c> sizing) so that downstream detection sees identical pixels.
/// Only the RGB channels are resampled; the output alpha channel is always opaque.
/// </summary>
internal static class PillowResampler
{
    private const int PrecisionBits = 32 - 8 - 2;
    private const double ThumbnailReducingGap = 2.0;
    private const double LanczosSupport = 3.0;
    private const double BilinearSupport = 1.0;

    /// <summary>
    /// Shrinks the image to fit inside the given bounds while keeping its aspect ratio, using Lanczos resampling.
    /// Returns the same instance when the image already fits.
    /// </summary>
    internal static RgbaImage Thumbnail(RgbaImage image, int maximumWidth, int maximumHeight)
    {
        if (maximumWidth >= image.Width && maximumHeight >= image.Height) return image;
        var aspect = (double)image.Width / image.Height;
        var targetWidth = maximumWidth;
        var targetHeight = maximumHeight;
        if ((double)maximumWidth / maximumHeight >= aspect) targetWidth = RoundAspect(maximumHeight * aspect, candidate => Math.Abs(aspect - (double)candidate / maximumHeight));
        else targetHeight = RoundAspect(maximumWidth / aspect, candidate => candidate == 0 ? 0 : Math.Abs(aspect - (double)maximumWidth / candidate));
        if (targetWidth == image.Width && targetHeight == image.Height) return image;
        return ResizeWithReducingGap(image, targetWidth, targetHeight, ResamplingFilter.Lanczos, ThumbnailReducingGap);
    }

    /// <summary>
    /// Resizes the whole image without a reducing pass. Returns the same instance when the size is unchanged.
    /// </summary>
    internal static RgbaImage Resize(RgbaImage image, int width, int height, ResamplingFilter filter)
    {
        if (width == image.Width && height == image.Height) return image;
        return Resample(image, width, height, filter, 0f, 0f, image.Width, image.Height);
    }

    private static int RoundAspect(double number, Func<int, double> distance)
    {
        var floor = (int)Math.Floor(number);
        var ceiling = (int)Math.Ceiling(number);
        var closest = distance(ceiling) < distance(floor) ? ceiling : floor;
        return Math.Max(closest, 1);
    }

    private static RgbaImage ResizeWithReducingGap(RgbaImage image, int width, int height, ResamplingFilter filter, double reducingGap)
    {
        var factorX = Math.Max(1, (int)((double)image.Width / width / reducingGap));
        var factorY = Math.Max(1, (int)((double)image.Height / height / reducingGap));
        if (factorX == 1 && factorY == 1) return Resample(image, width, height, filter, 0f, 0f, image.Width, image.Height);
        var reduceBox = GetSafeBox(image, width, height, filter);
        var reduced = Reduce(image, factorX, factorY, reduceBox);
        var boxLeft = (float)((double)(0 - reduceBox.Left) / factorX);
        var boxTop = (float)((double)(0 - reduceBox.Top) / factorY);
        var boxRight = (float)((double)(image.Width - reduceBox.Left) / factorX);
        var boxBottom = (float)((double)(image.Height - reduceBox.Top) / factorY);
        return Resample(reduced, width, height, filter, boxLeft, boxTop, boxRight, boxBottom);
    }

    // Expands the full-image box by the filter support so the reducing pass keeps every pixel the resampler reads.
    private static PixelRegion GetSafeBox(RgbaImage image, int width, int height, ResamplingFilter filter)
    {
        var filterSupport = GetSupport(filter) - 0.5;
        var supportX = filterSupport * ((double)image.Width / width);
        var supportY = filterSupport * ((double)image.Height / height);
        return new PixelRegion(Math.Max(0, (int)(0 - supportX)), Math.Max(0, (int)(0 - supportY)), Math.Min(image.Width, (int)Math.Ceiling(image.Width + supportX)), Math.Min(image.Height, (int)Math.Ceiling(image.Height + supportY)));
    }

    private static RgbaImage Reduce(RgbaImage image, int factorX, int factorY, PixelRegion box)
    {
        var outputWidth = (box.Width + factorX - 1) / factorX;
        var outputHeight = (box.Height + factorY - 1) / factorY;
        var source = image.Pixels;
        var result = new byte[outputWidth * outputHeight * RgbaImage.BytesPerPixel];
        for (var outputY = 0; outputY < outputHeight; outputY++)
        {
            var sourceTop = box.Top + outputY * factorY;
            var sourceBottom = Math.Min(sourceTop + factorY, box.Bottom);
            for (var outputX = 0; outputX < outputWidth; outputX++)
            {
                var sourceLeft = box.Left + outputX * factorX;
                var sourceRight = Math.Min(sourceLeft + factorX, box.Right);
                var count = (uint)((sourceRight - sourceLeft) * (sourceBottom - sourceTop));
                var amend = count / 2;
                var multiplier = (uint)(4294967296f / (float)(256u * count));
                uint red = amend, green = amend, blue = amend;
                for (var sourceY = sourceTop; sourceY < sourceBottom; sourceY++)
                {
                    var rowOffset = sourceY * image.Width;
                    for (var sourceX = sourceLeft; sourceX < sourceRight; sourceX++)
                    {
                        var offset = (rowOffset + sourceX) * RgbaImage.BytesPerPixel;
                        red += source[offset];
                        green += source[offset + 1];
                        blue += source[offset + 2];
                    }
                }
                var outputOffset = (outputY * outputWidth + outputX) * RgbaImage.BytesPerPixel;
                result[outputOffset] = (byte)((red * multiplier) >> 24);
                result[outputOffset + 1] = (byte)((green * multiplier) >> 24);
                result[outputOffset + 2] = (byte)((blue * multiplier) >> 24);
                result[outputOffset + 3] = byte.MaxValue;
            }
        }
        return new RgbaImage(outputWidth, outputHeight, result);
    }

    private static RgbaImage Resample(RgbaImage image, int width, int height, ResamplingFilter filter, float boxLeft, float boxTop, float boxRight, float boxBottom)
    {
        if (boxLeft - (int)boxLeft == 0 && boxRight - boxLeft == width && boxTop - (int)boxTop == 0 && boxBottom - boxTop == height) return image.Crop(new PixelRegion((int)boxLeft, (int)boxTop, (int)boxRight, (int)boxBottom));
        var needsHorizontalPass = width != image.Width || boxLeft != 0 || boxRight != width;
        var needsVerticalPass = height != image.Height || boxTop != 0 || boxBottom != height;
        var horizontal = PrecomputeCoefficients(image.Width, boxLeft, boxRight, width, filter);
        var vertical = PrecomputeCoefficients(image.Height, boxTop, boxBottom, height, filter);
        var firstSourceRow = vertical.Bounds[0];
        var lastSourceRow = vertical.Bounds[height * 2 - 2] + vertical.Bounds[height * 2 - 1];
        var current = image;
        if (needsHorizontalPass)
        {
            for (var row = 0; row < height; row++) vertical.Bounds[row * 2] -= firstSourceRow;
            current = ResampleHorizontal(image, width, firstSourceRow, lastSourceRow - firstSourceRow, horizontal);
        }
        if (needsVerticalPass) current = ResampleVertical(current, height, vertical);
        return ReferenceEquals(current, image) ? new RgbaImage(image.Width, image.Height, (byte[])image.Pixels.Clone()) : current;
    }

    private static Coefficients PrecomputeCoefficients(int inputSize, float inputStart, float inputEnd, int outputSize, ResamplingFilter filter)
    {
        var scale = (double)(inputEnd - inputStart) / outputSize;
        var filterScale = Math.Max(scale, 1.0);
        var support = GetSupport(filter) * filterScale;
        var kernelSize = (int)Math.Ceiling(support) * 2 + 1;
        var weights = new double[outputSize * kernelSize];
        var bounds = new int[outputSize * 2];
        for (var outputIndex = 0; outputIndex < outputSize; outputIndex++)
        {
            var center = inputStart + (outputIndex + 0.5) * scale;
            var weightSum = 0.0;
            var inverseFilterScale = 1.0 / filterScale;
            var minimum = Math.Max(0, (int)(center - support + 0.5));
            var maximum = Math.Min(inputSize, (int)(center + support + 0.5)) - minimum;
            var kernelOffset = outputIndex * kernelSize;
            for (var index = 0; index < maximum; index++)
            {
                var weight = Evaluate(filter, (index + minimum - center + 0.5) * inverseFilterScale);
                weights[kernelOffset + index] = weight;
                weightSum += weight;
            }
            if (weightSum != 0.0)
            {
                for (var index = 0; index < maximum; index++)
                {
                    weights[kernelOffset + index] /= weightSum;
                }
            }
            bounds[outputIndex * 2] = minimum;
            bounds[outputIndex * 2 + 1] = maximum;
        }
        var fixedPointWeights = new int[weights.Length];
        for (var index = 0; index < weights.Length; index++) fixedPointWeights[index] = weights[index] < 0 ? (int)(-0.5 + weights[index] * (1 << PrecisionBits)) : (int)(0.5 + weights[index] * (1 << PrecisionBits));
        return new Coefficients(kernelSize, bounds, fixedPointWeights);
    }

    private static RgbaImage ResampleHorizontal(RgbaImage image, int width, int rowOffset, int rowCount, Coefficients coefficients)
    {
        var source = image.Pixels;
        var result = new byte[width * rowCount * RgbaImage.BytesPerPixel];
        for (var row = 0; row < rowCount; row++)
        {
            var sourceRowOffset = (row + rowOffset) * image.Width;
            for (var outputX = 0; outputX < width; outputX++)
            {
                var minimum = coefficients.Bounds[outputX * 2];
                var count = coefficients.Bounds[outputX * 2 + 1];
                var kernelOffset = outputX * coefficients.KernelSize;
                var red = 1 << (PrecisionBits - 1);
                var green = red;
                var blue = red;
                for (var index = 0; index < count; index++)
                {
                    var weight = coefficients.Weights[kernelOffset + index];
                    var offset = (sourceRowOffset + minimum + index) * RgbaImage.BytesPerPixel;
                    red += source[offset] * weight;
                    green += source[offset + 1] * weight;
                    blue += source[offset + 2] * weight;
                }
                var outputOffset = (row * width + outputX) * RgbaImage.BytesPerPixel;
                result[outputOffset] = Clip(red);
                result[outputOffset + 1] = Clip(green);
                result[outputOffset + 2] = Clip(blue);
                result[outputOffset + 3] = byte.MaxValue;
            }
        }
        return new RgbaImage(width, rowCount, result);
    }

    private static RgbaImage ResampleVertical(RgbaImage image, int height, Coefficients coefficients)
    {
        var source = image.Pixels;
        var width = image.Width;
        var result = new byte[width * height * RgbaImage.BytesPerPixel];
        var accumulators = new int[width * 3];
        for (var outputY = 0; outputY < height; outputY++)
        {
            Array.Fill(accumulators, 1 << (PrecisionBits - 1));
            var minimum = coefficients.Bounds[outputY * 2];
            var count = coefficients.Bounds[outputY * 2 + 1];
            var kernelOffset = outputY * coefficients.KernelSize;
            for (var index = 0; index < count; index++)
            {
                var weight = coefficients.Weights[kernelOffset + index];
                var rowOffset = (minimum + index) * width * RgbaImage.BytesPerPixel;
                for (var x = 0; x < width; x++)
                {
                    var offset = rowOffset + x * RgbaImage.BytesPerPixel;
                    accumulators[x * 3] += source[offset] * weight;
                    accumulators[x * 3 + 1] += source[offset + 1] * weight;
                    accumulators[x * 3 + 2] += source[offset + 2] * weight;
                }
            }
            var outputRowOffset = outputY * width * RgbaImage.BytesPerPixel;
            for (var x = 0; x < width; x++)
            {
                var outputOffset = outputRowOffset + x * RgbaImage.BytesPerPixel;
                result[outputOffset] = Clip(accumulators[x * 3]);
                result[outputOffset + 1] = Clip(accumulators[x * 3 + 1]);
                result[outputOffset + 2] = Clip(accumulators[x * 3 + 2]);
                result[outputOffset + 3] = byte.MaxValue;
            }
        }
        return new RgbaImage(width, height, result);
    }

    private static byte Clip(int value) => (byte)Math.Clamp(value >> PrecisionBits, 0, byte.MaxValue);

    private static double GetSupport(ResamplingFilter filter) => filter == ResamplingFilter.Lanczos ? LanczosSupport : BilinearSupport;

    private static double Evaluate(ResamplingFilter filter, double x) => filter == ResamplingFilter.Lanczos ? Lanczos(x) : Bilinear(x);

    private static double Bilinear(double x)
    {
        if (x < 0.0) x = -x;
        return x < 1.0 ? 1.0 - x : 0.0;
    }

    private static double Lanczos(double x) => -3.0 <= x && x < 3.0 ? Sinc(x) * Sinc(x / 3) : 0.0;

    private static double Sinc(double x)
    {
        if (x == 0.0) return 1.0;
        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    private sealed record Coefficients(int KernelSize, int[] Bounds, int[] Weights);
}
