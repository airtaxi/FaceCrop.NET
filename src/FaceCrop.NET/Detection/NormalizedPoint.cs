namespace FaceCrop.Detection;

/// <summary>
/// A point expressed as a fraction of the image width and height.
/// </summary>
/// <param name="X">Horizontal position, where 0 is the left edge and 1 is the right edge.</param>
/// <param name="Y">Vertical position, where 0 is the top edge and 1 is the bottom edge.</param>
public readonly record struct NormalizedPoint(double X, double Y);
