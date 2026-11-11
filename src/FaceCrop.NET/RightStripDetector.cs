using FaceCrop.Imaging;
using FaceCrop.Internal;

namespace FaceCrop;

/// <summary>
/// Detects a flat-color panel attached to the right edge, such as a card's name strip.
/// The panel must have one consistent color and a consistent left boundary across most sampled rows;
/// lettering printed over it is tolerated. This is not a text detector, so unusual layouts may need an explicit width.
/// </summary>
public static class RightStripDetector
{
    private const int MaximumSampleWidth = 800;
    private const int EdgeColorTolerance = 22;
    private const int PanelColorTolerance = 25;

    /// <summary>
    /// Returns the exclusive right boundary of the usable area in source pixels, or the image width when no panel is found.
    /// </summary>
    /// <param name="image">The source image. Only the RGB channels are inspected.</param>
    public static int DetectUsableWidth(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var width = image.Width;
        var height = image.Height;
        var sampleWidth = Math.Min(width, MaximumSampleWidth);
        var sampleHeight = Math.Max(1, (int)Math.Round((double)((long)height * sampleWidth) / width));
        var sample = PillowResampler.Resize(image, sampleWidth, sampleHeight, ResamplingFilter.Bilinear);
        var rowStart = (int)(sampleHeight * 0.15);
        var rowEnd = Math.Min(sampleHeight, Math.Max((int)(sampleHeight * 0.85), rowStart + 1));
        var rowCount = rowEnd - rowStart;
        var edgeStart = Math.Max(0, (int)(sampleWidth * 0.985));
        var panelColor = GetMedianColor(sample, rowStart, rowEnd, edgeStart);
        var edgeMatches = 0;
        for (var y = rowStart; y < rowEnd; y++)
        {
            for (var x = edgeStart; x < sampleWidth; x++)
            {
                if (GetMaximumDifference(sample, x, y, panelColor) < EdgeColorTolerance)
                {
                    edgeMatches++;
                }
            }
        }
        if ((double)edgeMatches / (rowCount * (sampleWidth - edgeStart)) < 0.8) return width;
        var coverage = new double[sampleWidth];
        for (var x = 0; x < sampleWidth; x++)
        {
            var matches = 0;
            for (var y = rowStart; y < rowEnd; y++)
            {
                if (GetMaximumDifference(sample, x, y, panelColor) < PanelColorTolerance)
                {
                    matches++;
                }
            }
            coverage[x] = (double)matches / rowCount;
        }
        // Most rows must share the panel color right of the boundary and differ from it on the left.
        var radius = Math.Max(2, (int)Math.Round(sampleWidth * 0.006));
        var bestBoundary = -1;
        var bestContrast = double.NegativeInfinity;
        for (var x = (int)(sampleWidth * 0.65); x < (int)(sampleWidth * 0.97); x++)
        {
            var left = ArrayMath.Mean(coverage.AsSpan(Math.Max(0, x - radius), x - Math.Max(0, x - radius)));
            var right = ArrayMath.Mean(coverage.AsSpan(x, Math.Min(sampleWidth, x + radius) - x));
            if (!(left < 0.25 && right > 0.75 && ArrayMath.Mean(coverage.AsSpan(x)) > 0.65)) continue;
            var contrast = right - left;
            if (contrast >= bestContrast)
            {
                bestContrast = contrast;
                bestBoundary = x;
            }
        }
        if (bestBoundary < 0) return width;
        // A small guard removes antialiasing on the panel boundary.
        return Math.Max(1, (int)Math.Floor((double)((long)bestBoundary * width) / sampleWidth) - Math.Max(1, (int)Math.Ceiling((double)width / sampleWidth)));
    }

    private static float[] GetMedianColor(RgbaImage sample, int rowStart, int rowEnd, int edgeStart)
    {
        var count = (rowEnd - rowStart) * (sample.Width - edgeStart);
        var color = new float[3];
        var values = new float[count];
        for (var channel = 0; channel < color.Length; channel++)
        {
            var index = 0;
            for (var y = rowStart; y < rowEnd; y++)
            {
                for (var x = edgeStart; x < sample.Width; x++)
                {
                    values[index++] = sample.Pixels[(y * sample.Width + x) * RgbaImage.BytesPerPixel + channel];
                }
            }
            Array.Sort(values);
            color[channel] = count % 2 == 1 ? values[count / 2] : (values[count / 2 - 1] + values[count / 2]) / 2f;
        }
        return color;
    }

    private static float GetMaximumDifference(RgbaImage sample, int x, int y, float[] color)
    {
        var offset = (y * sample.Width + x) * RgbaImage.BytesPerPixel;
        return Math.Max(Math.Max(Math.Abs(sample.Pixels[offset] - color[0]), Math.Abs(sample.Pixels[offset + 1] - color[1])), Math.Abs(sample.Pixels[offset + 2] - color[2]));
    }
}
