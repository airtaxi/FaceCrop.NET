namespace FaceCrop;

/// <summary>
/// Which detected faces the crop must contain.
/// </summary>
public enum FaceSelectionMode
{
    /// <summary>
    /// Keep every detected face inside the crop.
    /// </summary>
    All,

    /// <summary>
    /// Keep only the face with the largest box.
    /// </summary>
    Largest,
}
