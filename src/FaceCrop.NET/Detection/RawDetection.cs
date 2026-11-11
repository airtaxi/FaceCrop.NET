namespace FaceCrop.Detection;

/// <summary>
/// A decoded candidate in coordinates normalized to the model input.
/// </summary>
internal sealed record RawDetection(float XMin, float YMin, float Width, float Height, float[] Keypoints, float Score)
{
    internal float XMax => XMin + Width;

    internal float YMax => YMin + Height;
}
