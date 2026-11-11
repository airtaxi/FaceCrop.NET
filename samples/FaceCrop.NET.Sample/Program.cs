using System.CommandLine;
using FaceCrop;
using FaceCrop.Imaging;
using FaceCrop.Sample;

var inputArgument = new Argument<DirectoryInfo>("input") { Description = "Directory containing the images to crop." };
var outputOption = new Option<DirectoryInfo?>("--output") { Description = "Output directory. Defaults to INPUT/cropped." };
var modelOption = new Option<FileInfo?>("--model") { Description = "BlazeFace short-range ONNX model. Defaults to the embedded model." };
var recursiveOption = new Option<bool>("--recursive") { Description = "Include images in subdirectories." };
var overwriteOption = new Option<bool>("--overwrite") { Description = "Replace existing output files." };
var noFaceOption = new Option<string>("--no-face") { Description = "What to do when no face is found.", DefaultValueFactory = _ => "skip" };
var facesOption = new Option<string>("--faces") { Description = "Faces the crop must contain.", DefaultValueFactory = _ => "all" };
var rightStripOption = new Option<string>("--right-strip") { Description = "Detect and exclude a flat-color panel on the right edge.", DefaultValueFactory = _ => "auto" };
var rightStripPixelsOption = new Option<int?>("--right-strip-px") { Description = "Width to exclude from the right edge, in pixels. Overrides --right-strip." };
var confidenceOption = new Option<double>("--confidence") { Description = "Minimum detection score.", DefaultValueFactory = _ => 0.5 };
var detectSizeOption = new Option<int>("--detect-size") { Description = "Maximum side of each detection window, in pixels.", DefaultValueFactory = _ => 1600 };
var tileDetectionOption = new Option<string>("--tile-detection") { Description = "When to scan overlapping tiles.", DefaultValueFactory = _ => "fallback" };
var minFaceVotesOption = new Option<int>("--min-face-votes") { Description = "Minimum number of distinct windows that must confirm a face when tiles are scanned.", DefaultValueFactory = _ => 2 };
var webpQualityOption = new Option<int>("--webp-quality") { Description = "Lossy WebP quality from 0 to 100.", DefaultValueFactory = _ => 85 };
var webpLosslessOption = new Option<bool>("--webp-lossless") { Description = "Save lossless WebP. --webp-quality is ignored." };
var backgroundOption = new Option<string?>("--background") { Description = "#RRGGBB color for transparent areas. Transparency is kept by default." };
noFaceOption.AcceptOnlyFromAmong("skip", "center");
facesOption.AcceptOnlyFromAmong("largest", "all");
rightStripOption.AcceptOnlyFromAmong("auto", "off");
tileDetectionOption.AcceptOnlyFromAmong("fallback", "always", "off");

var rootCommand = new RootCommand("Batch maximum-square face-aware cropping.")
{
    inputArgument,
    outputOption,
    modelOption,
    recursiveOption,
    overwriteOption,
    noFaceOption,
    facesOption,
    rightStripOption,
    rightStripPixelsOption,
    confidenceOption,
    detectSizeOption,
    tileDetectionOption,
    minFaceVotesOption,
    webpQualityOption,
    webpLosslessOption,
    backgroundOption,
};

rootCommand.SetAction(parseResult =>
{
    var input = parseResult.GetRequiredValue(inputArgument);
    var confidence = parseResult.GetValue(confidenceOption);
    var detectSize = parseResult.GetValue(detectSizeOption);
    var rightStripPixels = parseResult.GetValue(rightStripPixelsOption);
    var webpQuality = parseResult.GetValue(webpQualityOption);
    var minFaceVotes = parseResult.GetValue(minFaceVotesOption);
    var backgroundText = parseResult.GetValue(backgroundOption);
    var background = default(RgbColor);
    var isBackgroundValid = backgroundText is null || RgbColor.TryParseHex(backgroundText, out background);
    string? error = null;
    if (!input.Exists) error = "Input must be a directory.";
    else if (!(confidence > 0 && confidence <= 1) || detectSize < 128) error = "confidence: (0, 1], detect-size: >= 128";
    else if (rightStripPixels < 0) error = "right-strip-px must be >= 0";
    else if (webpQuality is < 0 or > 100) error = "webp-quality must be between 0 and 100";
    else if (minFaceVotes < 1) error = "min-face-votes must be >= 1";
    else if (!isBackgroundValid) error = "background must be #RRGGBB, for example \"#ffffff\"";
    if (error is not null)
    {
        Console.Error.WriteLine($"error: {error}");
        return 2;
    }
    var options = new FaceCropOptions
    {
        NoFace = parseResult.GetValue(noFaceOption) == "center" ? NoFaceMode.Center : NoFaceMode.Skip,
        FaceSelection = parseResult.GetValue(facesOption) == "largest" ? FaceSelectionMode.Largest : FaceSelectionMode.All,
        RightStrip = parseResult.GetValue(rightStripOption) == "off" ? RightStripMode.Off : RightStripMode.Auto,
        RightStripPixels = rightStripPixels,
        MinDetectionConfidence = (float)confidence,
        DetectSize = detectSize,
        TileDetection = parseResult.GetValue(tileDetectionOption) switch
        {
            "always" => TileDetectionMode.Always,
            "off" => TileDetectionMode.Off,
            _ => TileDetectionMode.Fallback,
        },
        MinFaceVotes = minFaceVotes,
        Background = backgroundText is null ? null : background,
        Webp = new WebpEncodeOptions { Quality = webpQuality, Lossless = parseResult.GetValue(webpLosslessOption) },
        ModelPath = parseResult.GetValue(modelOption)?.FullName,
    };
    var settings = new BatchCropSettings(input.FullName, parseResult.GetValue(outputOption)?.FullName, parseResult.GetValue(recursiveOption), parseResult.GetValue(overwriteOption), options);
    return BatchCropRunner.Run(settings);
});

var parseResult = rootCommand.Parse(args);
var exitCode = parseResult.Invoke();
return parseResult.Errors.Count > 0 ? 2 : exitCode;
