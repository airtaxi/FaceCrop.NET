using FaceCrop.Detection;
using FaceCrop.Imaging;

namespace FaceCrop;

/// <summary>
/// Finds faces and plans the largest square crop that keeps them inside, after removing the configured margins from each edge.
/// Instances are safe to use from multiple threads.
/// </summary>
public sealed class FaceCropper : IDisposable
{
    private const int MinimumDetectSize = 128;
    private const double FullPercent = 100;
    private readonly BlazeFaceDetector _detector;
    private readonly TiledFaceDetector _tiledDetector;

    /// <summary>
    /// Loads the face detection model.
    /// </summary>
    /// <param name="options">Crop settings, or <see langword="null"/> for the defaults.</param>
    public FaceCropper(FaceCropOptions? options = null)
    {
        Options = options ?? new FaceCropOptions();
        Validate(Options);
        _detector = new BlazeFaceDetector(new BlazeFaceDetectorOptions { ModelPath = Options.ModelPath, MinDetectionConfidence = Options.MinDetectionConfidence });
        _tiledDetector = new TiledFaceDetector(_detector);
    }

    /// <summary>
    /// The settings in use.
    /// </summary>
    public FaceCropOptions Options { get; }

    /// <summary>
    /// Detects faces and plans the crop. Failures are reported through <see cref="FaceCropStatus.Error"/> instead of exceptions.
    /// </summary>
    /// <param name="image">The source image.</param>
    public FaceCropResult Analyze(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var progress = new Progress();
        try { return Analyze(image, progress); }
        catch (Exception exception) { return progress.ToError(exception); }
    }

    /// <summary>
    /// Cuts the planned region out of the image and fills transparent areas when <see cref="FaceCropOptions.Background"/> is set.
    /// </summary>
    /// <param name="image">The source image that was analyzed.</param>
    /// <param name="cropBox">The region to cut out, usually <see cref="FaceCropResult.CropBox"/>.</param>
    public RgbaImage Crop(RgbaImage image, PixelRegion cropBox)
    {
        ArgumentNullException.ThrowIfNull(image);
        var cropped = image.Crop(cropBox);
        return Options.Background is { } background ? AlphaCompositor.Flatten(cropped, background) : cropped;
    }

    /// <summary>
    /// Reads an image file, plans the crop and writes the cropped image as WebP, keeping the source ICC profile.
    /// Nothing is written when the result has no crop box. Failures are reported through <see cref="FaceCropStatus.Error"/> instead of exceptions.
    /// </summary>
    /// <param name="sourcePath">The image to crop.</param>
    /// <param name="destinationPath">The WebP file to write. Missing directories are created.</param>
    public FaceCropResult CropFile(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);
        var progress = new Progress();
        try
        {
            var loaded = ImageFile.Load(sourcePath);
            var result = Analyze(loaded.Image, progress);
            if (result.CropBox is { } cropBox) ImageFile.SaveWebp(Crop(loaded.Image, cropBox), destinationPath, Options.Webp, loaded.IccProfile);
            return result;
        }
        catch (Exception exception) { return progress.ToError(exception); }
    }

    /// <summary>
    /// Releases the face detection model.
    /// </summary>
    public void Dispose() => _detector.Dispose();

    private FaceCropResult Analyze(RgbaImage image, Progress progress)
    {
        var width = image.Width;
        var height = image.Height;
        progress.ImageWidth = width;
        progress.ImageHeight = height;
        var usableRegion = GetUsableRegion(width, height);
        progress.UsableRegion = usableRegion;
        var detection = _tiledDetector.Detect(image, usableRegion, Options.DetectSize, Options.TileDetection, Options.MinFaceVotes);
        progress.Faces = detection.Faces;
        var detail = $"detection_pass={detection.DetectionPass}";
        if (detection.Faces.Count == 0)
        {
            if (Options.NoFace == NoFaceMode.Skip) return progress.ToResult(FaceCropStatus.NoFace, detail);
            var centerX = (usableRegion.Left + usableRegion.Right) / 2.0;
            var centerY = (usableRegion.Top + usableRegion.Bottom) / 2.0;
            progress.CropBox = SquareCropPlanner.CropBox(usableRegion, centerX, centerY, centerX, centerY, centerX, centerY);
            return progress.ToResult(FaceCropStatus.CenterFallback, detail);
        }
        IReadOnlyList<FaceCandidate> selected = Options.FaceSelection == FaceSelectionMode.All ? detection.Faces : [detection.Faces.MaxBy(face => face.Box.Width * face.Box.Height)!];
        try
        {
            progress.CropBox = SquareCropPlanner.PlanFaceCrop(selected, width, height, usableRegion);
            return progress.ToResult(FaceCropStatus.FaceCrop, detail);
        }
        catch (FaceRegionDoesNotFitException exception)
        {
            var recovery = SquareCropPlanner.RecoverLowConfidence(selected, width, height, usableRegion, exception.Message);
            (progress.CropBox, progress.Recovery) = (recovery.Box, recovery);
            return progress.ToResult(FaceCropStatus.FaceCropLowConfidenceFiltered, $"{detail}; {recovery.ToJson()}");
        }
    }

    private PixelRegion GetUsableRegion(int width, int height)
    {
        var usableRegion = new PixelRegion(GetMarginPixels(width, Options.MarginLeftPercent), GetMarginPixels(height, Options.MarginTopPercent), width - GetMarginPixels(width, Options.MarginRightPercent), height - GetMarginPixels(height, Options.MarginBottomPercent));
        if (usableRegion.Width <= 0 || usableRegion.Height <= 0) throw new FaceCropException("margins_exclude_entire_image");
        return usableRegion;
    }

    private static int GetMarginPixels(int length, double percent) => (int)Math.Round(length * percent / FullPercent, MidpointRounding.AwayFromZero);

    private static void Validate(FaceCropOptions options)
    {
        if (!(options.MinDetectionConfidence > 0f && options.MinDetectionConfidence <= 1f)) throw new ArgumentOutOfRangeException(nameof(options), options.MinDetectionConfidence, "MinDetectionConfidence must be in the range (0, 1].");
        if (options.DetectSize < MinimumDetectSize) throw new ArgumentOutOfRangeException(nameof(options), options.DetectSize, "DetectSize must be at least 128.");
        if (options.MarginTopPercent is not >= 0) throw new ArgumentOutOfRangeException(nameof(options), options.MarginTopPercent, "MarginTopPercent must not be negative.");
        if (options.MarginBottomPercent is not >= 0) throw new ArgumentOutOfRangeException(nameof(options), options.MarginBottomPercent, "MarginBottomPercent must not be negative.");
        if (options.MarginLeftPercent is not >= 0) throw new ArgumentOutOfRangeException(nameof(options), options.MarginLeftPercent, "MarginLeftPercent must not be negative.");
        if (options.MarginRightPercent is not >= 0) throw new ArgumentOutOfRangeException(nameof(options), options.MarginRightPercent, "MarginRightPercent must not be negative.");
        if (options.MarginTopPercent + options.MarginBottomPercent is not < FullPercent) throw new ArgumentOutOfRangeException(nameof(options), options.MarginTopPercent + options.MarginBottomPercent, "MarginTopPercent and MarginBottomPercent must add up to less than 100.");
        if (options.MarginLeftPercent + options.MarginRightPercent is not < FullPercent) throw new ArgumentOutOfRangeException(nameof(options), options.MarginLeftPercent + options.MarginRightPercent, "MarginLeftPercent and MarginRightPercent must add up to less than 100.");
        if (options.MinFaceVotes < 1) throw new ArgumentOutOfRangeException(nameof(options), options.MinFaceVotes, "MinFaceVotes must be at least 1.");
        ArgumentNullException.ThrowIfNull(options.Webp, nameof(options));
        if (options.Webp.Quality is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(options), options.Webp.Quality, "Webp.Quality must be between 0 and 100.");
    }

    // Collects values as they are computed so that a failure can still report them.
    private sealed class Progress
    {
        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public PixelRegion? UsableRegion { get; set; }

        public IReadOnlyList<FaceCandidate> Faces { get; set; } = [];

        public PixelRegion? CropBox { get; set; }

        public LowConfidenceRecovery? Recovery { get; set; }

        public FaceCropResult ToResult(FaceCropStatus status, string detail) => new()
        {
            Status = status,
            Detail = detail,
            CropBox = CropBox,
            Faces = Faces,
            ImageWidth = ImageWidth,
            ImageHeight = ImageHeight,
            UsableRegion = UsableRegion,
            Recovery = Recovery
        };

        public FaceCropResult ToError(Exception exception) => ToResult(FaceCropStatus.Error, exception.Message) with { Exception = exception };
    }
}
