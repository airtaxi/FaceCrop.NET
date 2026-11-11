namespace FaceCrop;

/// <summary>
/// The required face region cannot fit inside the largest square crop.
/// </summary>
/// <param name="message">The failure reason, starting with <c>face_region_does_not_fit:</c>.</param>
public sealed class FaceRegionDoesNotFitException(string message) : FaceCropException(message);
