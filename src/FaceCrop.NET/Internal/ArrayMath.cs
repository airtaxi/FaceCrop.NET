namespace FaceCrop.Internal;

/// <summary>
/// Floating-point reductions that sum in the same order as NumPy's pairwise summation,
/// so threshold comparisons on averaged values produce identical decisions.
/// </summary>
internal static class ArrayMath
{
    private const int UnrollCount = 8;
    private const int PairwiseBlockSize = 128;

    internal static double Mean(ReadOnlySpan<double> values) => values.Length == 0 ? double.NaN : PairwiseSum(values) / values.Length;

    internal static double PairwiseSum(ReadOnlySpan<double> values)
    {
        if (values.Length < UnrollCount)
        {
            var total = -0.0;
            foreach (var value in values) total += value;
            return total;
        }
        if (values.Length <= PairwiseBlockSize)
        {
            Span<double> accumulators = stackalloc double[UnrollCount];
            values[..UnrollCount].CopyTo(accumulators);
            var index = UnrollCount;
            for (; index < values.Length - values.Length % UnrollCount; index += UnrollCount)
            {
                for (var lane = 0; lane < UnrollCount; lane++)
                {
                    accumulators[lane] += values[index + lane];
                }
            }
            var sum = accumulators[0] + accumulators[1] + (accumulators[2] + accumulators[3]) + (accumulators[4] + accumulators[5] + (accumulators[6] + accumulators[7]));
            for (; index < values.Length; index++) sum += values[index];
            return sum;
        }
        var half = values.Length / 2;
        half -= half % UnrollCount;
        return PairwiseSum(values[..half]) + PairwiseSum(values[half..]);
    }
}
