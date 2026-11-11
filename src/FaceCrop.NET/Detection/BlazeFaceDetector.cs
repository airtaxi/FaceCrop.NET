using FaceCrop.Imaging;
using Microsoft.ML.OnnxRuntime;

namespace FaceCrop.Detection;

/// <summary>
/// Finds faces with the BlazeFace short-range model. Pre- and post-processing reproduce MediaPipe's face detector:
/// letterboxed 128x128 input, SSD anchor decoding, score sigmoid and weighted non-maximum suppression.
/// Instances are safe to use from multiple threads.
/// </summary>
public sealed class BlazeFaceDetector : IDisposable
{
    private const string EmbeddedModelName = "FaceCrop.Models.blaze_face_short_range.onnx";
    private const int CoordinateCount = 16;
    private const int KeypointCount = 6;
    private const int KeypointOffset = 4;
    private const float CoordinateScale = TensorImageConverter.InputSize;
    private const float ScoreClippingThreshold = 100f;

    private static readonly long[] s_inputShape = [1, TensorImageConverter.InputSize, TensorImageConverter.InputSize, TensorImageConverter.ChannelCount];
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _boxOutputName;
    private readonly string _scoreOutputName;
    private readonly float _minimumDetectionConfidence;
    private readonly float _minimumSuppressionThreshold;

    /// <summary>
    /// Loads the model and prepares an inference session.
    /// </summary>
    /// <param name="options">Detector settings, or <see langword="null"/> for the defaults.</param>
    public BlazeFaceDetector(BlazeFaceDetectorOptions? options = null)
    {
        options ??= new BlazeFaceDetectorOptions();
        if (!(options.MinDetectionConfidence > 0f && options.MinDetectionConfidence <= 1f)) throw new ArgumentOutOfRangeException(nameof(options), options.MinDetectionConfidence, "MinDetectionConfidence must be in the range (0, 1].");
        _minimumDetectionConfidence = options.MinDetectionConfidence;
        _minimumSuppressionThreshold = options.MinSuppressionThreshold;
        _session = options.ModelPath is null ? new InferenceSession(ReadEmbeddedModel()) : new InferenceSession(options.ModelPath);
        try
        {
            _inputName = _session.InputMetadata.Keys.Single();
            (_boxOutputName, _scoreOutputName) = ResolveOutputNames(_session);
        }
        catch
        {
            _session.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Detects faces in the RGB channels of an image. The alpha channel is ignored.
    /// </summary>
    /// <param name="image">The image to search.</param>
    /// <returns>Detections ordered by descending score.</returns>
    public IReadOnlyList<FaceDetection> Detect(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var letterbox = LetterboxTransform.Create(image.Width, image.Height);
        var tensor = TensorImageConverter.Convert(image, letterbox);
        using var inputValue = OrtValue.CreateTensorValueFromMemory(tensor, s_inputShape);
        using var runOptions = new RunOptions();
        using var outputs = _session.Run(runOptions, [_inputName], [inputValue], [_boxOutputName, _scoreOutputName]);
        var outputValues = outputs.ToList();
        var candidates = DecodeCandidates(outputValues[0].GetTensorDataAsSpan<float>(), outputValues[1].GetTensorDataAsSpan<float>());
        var merged = WeightedNonMaxSuppression.Apply(candidates, _minimumSuppressionThreshold);
        return [.. merged.Select(detection => ToFaceDetection(detection, letterbox, image.Width, image.Height))];
    }

    /// <summary>
    /// Releases the inference session.
    /// </summary>
    public void Dispose() => _session.Dispose();

    private List<RawDetection> DecodeCandidates(ReadOnlySpan<float> rawBoxes, ReadOnlySpan<float> rawScores)
    {
        var anchors = SsdAnchors.ShortRange;
        if (rawScores.Length != anchors.Length || rawBoxes.Length != anchors.Length * CoordinateCount) throw new InvalidOperationException("The model output does not match the BlazeFace short-range layout.");
        var detections = new List<RawDetection>();
        for (var index = 0; index < anchors.Length; index++)
        {
            var logit = Math.Clamp(rawScores[index], -ScoreClippingThreshold, ScoreClippingThreshold);
            var score = 1.0f / (1.0f + MathF.Exp(-logit));
            if (score < _minimumDetectionConfidence) continue;
            var anchor = anchors[index];
            var offset = index * CoordinateCount;
            var centerX = rawBoxes[offset] / CoordinateScale + anchor.CenterX;
            var centerY = rawBoxes[offset + 1] / CoordinateScale + anchor.CenterY;
            var width = rawBoxes[offset + 2] / CoordinateScale;
            var height = rawBoxes[offset + 3] / CoordinateScale;
            var xMin = centerX - width / 2.0f;
            var yMin = centerY - height / 2.0f;
            var boxWidth = centerX + width / 2.0f - xMin;
            var boxHeight = centerY + height / 2.0f - yMin;
            if (boxWidth < 0 || boxHeight < 0 || float.IsNaN(boxWidth) || float.IsNaN(boxHeight)) continue;
            var keypoints = new float[KeypointCount * 2];
            for (var keypoint = 0; keypoint < KeypointCount; keypoint++)
            {
                var keypointOffset = offset + KeypointOffset + keypoint * 2;
                keypoints[keypoint * 2] = rawBoxes[keypointOffset] / CoordinateScale + anchor.CenterX;
                keypoints[keypoint * 2 + 1] = rawBoxes[keypointOffset + 1] / CoordinateScale + anchor.CenterY;
            }
            detections.Add(new RawDetection(xMin, yMin, boxWidth, boxHeight, keypoints, score));
        }
        return detections;
    }

    private static FaceDetection ToFaceDetection(RawDetection detection, LetterboxTransform letterbox, int imageWidth, int imageHeight)
    {
        var keypoints = new NormalizedPoint[KeypointCount];
        for (var index = 0; index < KeypointCount; index++)
        {
            var (x, y) = letterbox.Project(detection.Keypoints[index * 2], detection.Keypoints[index * 2 + 1], imageWidth, imageHeight);
            keypoints[index] = new NormalizedPoint(x, y);
        }
        (float X, float Y)[] corners =
        [
            letterbox.Project(detection.XMin, detection.YMin, imageWidth, imageHeight),
            letterbox.Project(detection.XMin + detection.Width, detection.YMin, imageWidth, imageHeight),
            letterbox.Project(detection.XMin + detection.Width, detection.YMin + detection.Height, imageWidth, imageHeight),
            letterbox.Project(detection.XMin, detection.YMin + detection.Height, imageWidth, imageHeight),
        ];
        var left = corners.Min(corner => corner.X);
        var top = corners.Min(corner => corner.Y);
        var right = corners.Max(corner => corner.X);
        var bottom = corners.Max(corner => corner.Y);
        var box = new DetectionBox(ToBoundedPixel(left * imageWidth, imageWidth), ToBoundedPixel(top * imageHeight, imageHeight), ToBoundedPixel((right - left) * imageWidth, imageWidth), ToBoundedPixel((bottom - top) * imageHeight, imageHeight));
        return new FaceDetection(box, keypoints, detection.Score);
    }

    // Truncates toward zero and clamps to [0, bound], as MediaPipe does when converting relative boxes to pixels.
    private static int ToBoundedPixel(float value, int bound) => Math.Max(0, Math.Min((int)value, bound));

    private static (string BoxOutputName, string ScoreOutputName) ResolveOutputNames(InferenceSession session)
    {
        string? boxOutputName = null;
        string? scoreOutputName = null;
        foreach (var (name, metadata) in session.OutputMetadata)
        {
            var lastDimension = metadata.Dimensions[^1];
            if (lastDimension == CoordinateCount) boxOutputName = name;
            else if (lastDimension == 1) scoreOutputName = name;
        }
        if (boxOutputName is null || scoreOutputName is null) throw new InvalidOperationException("The model does not expose BlazeFace box and score outputs.");
        return (boxOutputName, scoreOutputName);
    }

    private static byte[] ReadEmbeddedModel()
    {
        using var stream = typeof(BlazeFaceDetector).Assembly.GetManifestResourceStream(EmbeddedModelName) ?? throw new InvalidOperationException("The embedded face detection model is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
