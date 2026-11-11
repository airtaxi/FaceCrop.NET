namespace FaceCrop.Detection;

/// <summary>
/// A face bounding box in whole pixels, clamped to the detector input image.
/// </summary>
/// <param name="OriginX">Left edge in pixels.</param>
/// <param name="OriginY">Top edge in pixels.</param>
/// <param name="Width">Box width in pixels.</param>
/// <param name="Height">Box height in pixels.</param>
public readonly record struct DetectionBox(int OriginX, int OriginY, int Width, int Height);
