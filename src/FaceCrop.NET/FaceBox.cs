namespace FaceCrop;

/// <summary>
/// A face box in source image pixels.
/// </summary>
/// <param name="OriginX">Left edge.</param>
/// <param name="OriginY">Top edge.</param>
/// <param name="Width">Box width.</param>
/// <param name="Height">Box height.</param>
public readonly record struct FaceBox(double OriginX, double OriginY, double Width, double Height);
