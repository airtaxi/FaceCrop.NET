namespace FaceCrop.Detection;

/// <summary>
/// The square region, centered on the image and padded on the short side, that is mapped onto the model input.
/// All arithmetic is single precision, as in MediaPipe's image-to-tensor stage.
/// </summary>
internal readonly record struct LetterboxTransform(float CenterX, float CenterY, float Width, float Height)
{
    internal static LetterboxTransform Create(int imageWidth, int imageHeight)
    {
        const float TensorAspectRatio = (float)TensorImageConverter.InputSize / TensorImageConverter.InputSize;
        var centerX = 0.5f * imageWidth;
        var centerY = 0.5f * imageHeight;
        float width = imageWidth;
        float height = imageHeight;
        var regionAspectRatio = height / width;
        if (TensorAspectRatio > regionAspectRatio) return new LetterboxTransform(centerX, centerY, width, width * TensorAspectRatio);
        return new LetterboxTransform(centerX, centerY, height / TensorAspectRatio, height);
    }

    /// <summary>
    /// Maps a point normalized to the model input back to coordinates normalized to the source image.
    /// </summary>
    internal (float X, float Y) Project(float x, float y, int imageWidth, int imageHeight)
    {
        const float Cosine = 1f;
        const float Sine = 0f;
        var inverseWidth = 1.0f / imageWidth;
        var inverseHeight = 1.0f / imageHeight;
        var matrix0 = Width * Cosine * inverseWidth;
        var matrix1 = -Height * Sine * inverseWidth;
        var matrix3 = (-0.5f * Width * Cosine + 0.5f * Height * Sine + CenterX) * inverseWidth;
        var matrix4 = Width * Sine * inverseHeight;
        var matrix5 = Height * Cosine * inverseHeight;
        var matrix7 = (-0.5f * Height * Cosine - 0.5f * Width * Sine + CenterY) * inverseHeight;
        return (x * matrix0 + y * matrix1 + matrix3, x * matrix4 + y * matrix5 + matrix7);
    }
}
