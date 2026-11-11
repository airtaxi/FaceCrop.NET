using FaceCrop.Imaging;

namespace FaceCrop;

/// <summary>
/// A planned square crop.
/// </summary>
/// <param name="Box">The square crop region in source image pixels.</param>
/// <param name="Status">How the crop was obtained.</param>
/// <param name="Detail">Notes about clipping or partial crops; empty when there is nothing to report.</param>
public sealed record FaceCropPlan(PixelRegion Box, FaceCropStatus Status, string Detail);
