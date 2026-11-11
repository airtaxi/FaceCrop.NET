namespace FaceCrop.Detection;

/// <summary>
/// Generates the SSD anchors of the BlazeFace short-range model: four layers with strides 8, 16, 16 and 16,
/// two fixed-size anchors per layer and cell, 896 anchors in total.
/// </summary>
internal static class SsdAnchors
{
    private const int InputSize = TensorImageConverter.InputSize;
    private const float AnchorOffset = 0.5f;
    private const int AnchorsPerLayer = 2;
    private static readonly int[] s_strides = [8, 16, 16, 16];

    internal static AnchorPoint[] ShortRange { get; } = Generate();

    private static AnchorPoint[] Generate()
    {
        var anchors = new List<AnchorPoint>();
        var layer = 0;
        while (layer < s_strides.Length)
        {
            var anchorsPerCell = 0;
            var lastSameStrideLayer = layer;
            while (lastSameStrideLayer < s_strides.Length && s_strides[lastSameStrideLayer] == s_strides[layer])
            {
                anchorsPerCell += AnchorsPerLayer;
                lastSameStrideLayer++;
            }
            var featureMapSize = (int)MathF.Ceiling(1.0f * InputSize / s_strides[layer]);
            for (var y = 0; y < featureMapSize; y++)
            {
                for (var x = 0; x < featureMapSize; x++)
                {
                    var centerX = (x + AnchorOffset) * 1.0f / featureMapSize;
                    var centerY = (y + AnchorOffset) * 1.0f / featureMapSize;
                    for (var anchor = 0; anchor < anchorsPerCell; anchor++) anchors.Add(new AnchorPoint(centerX, centerY));
                }
            }
            layer = lastSameStrideLayer;
        }
        return [.. anchors];
    }
}
