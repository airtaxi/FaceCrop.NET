using System.Globalization;
using FaceCrop.Detection;
using FaceCrop.Imaging;

namespace FaceCrop;

/// <summary>
/// Detects faces on the full image and, when needed, on overlapping square tiles at two scales.
/// Tile hits of the same face are merged, and a face is kept only when enough distinct windows confirm it.
/// Transparent pixels are composited over white before detection.
/// </summary>
/// <param name="detector">The detector that scans each window.</param>
public sealed class TiledFaceDetector(BlazeFaceDetector detector)
{
    private static readonly double[] s_tileScales = [1.0, 0.6];
    private readonly BlazeFaceDetector _detector = detector ?? throw new ArgumentNullException(nameof(detector));

    /// <summary>
    /// Detects faces and maps them to source image coordinates.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="detectSize">Each window is shrunk to fit inside this many pixels per side before detection.</param>
    /// <param name="mode">Whether tiles are scanned.</param>
    /// <param name="minimumVotes">Minimum number of distinct windows that must find a face when tiles are scanned.</param>
    public TiledDetectionResult Detect(RgbaImage image, int detectSize = 1600, TileDetectionMode mode = TileDetectionMode.Fallback, int minimumVotes = 2)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(detectSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumVotes, 1);
        var flattened = AlphaCompositor.Flatten(image, RgbColor.White);
        var width = image.Width;
        var height = image.Height;
        var faces = DetectWindow(flattened, new PixelRegion(0, 0, width, height), detectSize);
        if (mode == TileDetectionMode.Off || (faces.Count == 1 && mode == TileDetectionMode.Fallback)) return new TiledDetectionResult(faces, "full", faces.Count, 0);
        // Both scales cover the whole image; overlapping windows avoid misses at tile edges.
        foreach (var scale in s_tileScales)
        {
            var side = Math.Max(1, (int)Math.Round(Math.Min(width, height) * scale));
            foreach (var top in GetWindowStarts(height, side))
            {
                foreach (var left in GetWindowStarts(width, side))
                {
                    faces.AddRange(DetectWindow(flattened, new PixelRegion(left, top, left + side, top + side), detectSize));
                }
            }
        }
        var groups = new List<List<FaceCandidate>>();
        foreach (var face in faces.OrderByDescending(face => face.Score))
        {
            var group = groups.FirstOrDefault(group => IsSameFace(face, group[0]));
            if (group is null) groups.Add([face]);
            else group.Add(face);
        }
        var confirmed = new List<FaceCandidate>();
        foreach (var group in groups)
        {
            var representative = group[0] with { SupportWindows = group.Select(face => face.Region).Distinct().Count() };
            if (representative.SupportWindows >= minimumVotes) confirmed.Add(representative);
        }
        var rejected = groups.Count - confirmed.Count;
        return new TiledDetectionResult(confirmed, string.Create(CultureInfo.InvariantCulture, $"full_and_tiles; rejected_unconfirmed={rejected}; raw_detections={faces.Count}"), faces.Count, rejected);
    }

    private List<FaceCandidate> DetectWindow(RgbaImage flattened, PixelRegion region, int detectSize)
    {
        var isFullImage = region.Left == 0 && region.Top == 0 && region.Width == flattened.Width && region.Height == flattened.Height;
        var preview = PillowResampler.Thumbnail(isFullImage ? flattened : flattened.Crop(region), detectSize, detectSize);
        var scaleX = (double)region.Width / preview.Width;
        var scaleY = (double)region.Height / preview.Height;
        var faces = new List<FaceCandidate>();
        foreach (var detection in _detector.Detect(preview))
        {
            var box = detection.BoundingBox;
            var faceBox = new FaceBox(region.Left + box.OriginX * scaleX, region.Top + box.OriginY * scaleY, box.Width * scaleX, box.Height * scaleY);
            var keypoints = detection.Keypoints.Select(point => new NormalizedPoint((region.Left + point.X * region.Width) / flattened.Width, (region.Top + point.Y * region.Height) / flattened.Height)).ToArray();
            faces.Add(new FaceCandidate(faceBox, keypoints, [detection.Score], region, 1));
        }
        return faces;
    }

    private static SortedSet<int> GetWindowStarts(int length, int side)
    {
        var starts = new SortedSet<int> { length - side };
        var step = Math.Max(1, side / 2);
        for (var start = 0; start <= length - side; start += step) starts.Add(start);
        return starts;
    }

    private static bool IsSameFace(FaceCandidate first, FaceCandidate second)
    {
        var a = first.Box;
        var b = second.Box;
        var intersectionWidth = Math.Max(0, Math.Min(a.OriginX + a.Width, b.OriginX + b.Width) - Math.Max(a.OriginX, b.OriginX));
        var intersectionHeight = Math.Max(0, Math.Min(a.OriginY + a.Height, b.OriginY + b.Height) - Math.Max(a.OriginY, b.OriginY));
        var intersection = intersectionWidth * intersectionHeight;
        var firstArea = a.Width * a.Height;
        var secondArea = b.Width * b.Height;
        var intersectionOverUnion = intersection / Math.Max(1e-9, firstArea + secondArea - intersection);
        var containment = intersection / Math.Max(1e-9, Math.Min(firstArea, secondArea));
        var nearCenter = Math.Abs(a.OriginX + a.Width / 2 - b.OriginX - b.Width / 2) < 0.25 * Math.Min(a.Width, b.Width) && Math.Abs(a.OriginY + a.Height / 2 - b.OriginY - b.Height / 2) < 0.25 * Math.Min(a.Height, b.Height);
        return intersectionOverUnion > 0.3 || (containment > 0.8 && nearCenter);
    }
}
