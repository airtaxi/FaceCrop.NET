namespace FaceCrop.Sample;

/// <summary>
/// Settings for one batch run.
/// </summary>
/// <param name="InputDirectory">Directory containing the source images.</param>
/// <param name="OutputDirectory">Directory for cropped images and the report, or <see langword="null"/> for <c>INPUT/cropped</c>.</param>
/// <param name="Recursive">Include images in subdirectories.</param>
/// <param name="Overwrite">Replace existing output files.</param>
/// <param name="Options">Crop settings.</param>
internal sealed record BatchCropSettings(string InputDirectory, string? OutputDirectory, bool Recursive, bool Overwrite, FaceCropOptions Options);
