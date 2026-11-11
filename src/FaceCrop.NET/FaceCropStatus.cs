namespace FaceCrop;

/// <summary>
/// Outcome of processing one image.
/// </summary>
public enum FaceCropStatus
{
    /// <summary>
    /// The crop contains the selected faces.
    /// </summary>
    FaceCrop,

    /// <summary>
    /// The selected faces overlap the excluded right panel, so the face region was clipped at its boundary.
    /// </summary>
    FaceCropStripClipped,

    /// <summary>
    /// The faces did not fit together, so lower-confidence faces were dropped until they did.
    /// </summary>
    FaceCropLowConfidenceFiltered,

    /// <summary>
    /// No face was found and the centered square was used.
    /// </summary>
    CenterFallback,

    /// <summary>
    /// No face was found and no crop was produced.
    /// </summary>
    NoFace,

    /// <summary>
    /// Processing failed.
    /// </summary>
    Error,
}
