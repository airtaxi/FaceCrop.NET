namespace FaceCrop.Imaging;

/// <summary>
/// A decoded, orientation-corrected image together with its embedded color profile.
/// </summary>
/// <param name="Image">Decoded RGBA pixels.</param>
/// <param name="IccProfile">The embedded ICC profile, or <see langword="null"/> when the source has none.</param>
public sealed record LoadedImage(RgbaImage Image, byte[]? IccProfile);
