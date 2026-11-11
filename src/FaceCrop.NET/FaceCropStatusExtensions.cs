namespace FaceCrop;

/// <summary>
/// Report codes for <see cref="FaceCropStatus"/>.
/// </summary>
public static class FaceCropStatusExtensions
{
    extension(FaceCropStatus status)
    {
        /// <summary>
        /// The snake_case code used in reports, such as <c>face_crop</c> or <c>no_face</c>.
        /// </summary>
        public string Code => status switch
        {
            FaceCropStatus.FaceCrop => "face_crop",
            FaceCropStatus.FaceCropStripClipped => "face_crop_strip_clipped",
            FaceCropStatus.FaceCropLowConfidenceFiltered => "face_crop_low_confidence_filtered",
            FaceCropStatus.CenterFallback => "center_fallback",
            FaceCropStatus.NoFace => "no_face",
            FaceCropStatus.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }
}
