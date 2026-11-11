namespace FaceCrop;

/// <summary>
/// What to do when no face is found.
/// </summary>
public enum NoFaceMode
{
    /// <summary>
    /// Produce no crop.
    /// </summary>
    Skip,

    /// <summary>
    /// Crop the largest centered square of the usable area.
    /// </summary>
    Center,
}
