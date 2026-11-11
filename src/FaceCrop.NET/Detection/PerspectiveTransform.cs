namespace FaceCrop.Detection;

/// <summary>
/// Computes the destination-to-source matrix used to sample the model input, following OpenCV's
/// <c>RotatedRect::points</c>, <c>getPerspectiveTransform</c> (LU solve) and 3x3 <c>invert</c> step by step.
/// Matching their rounding matters: sample positions that land exactly between two 1/32-pixel steps
/// are resolved by the last bits of this matrix.
/// </summary>
internal static class PerspectiveTransform
{
    private const int UnknownCount = 8;
    private const double MachineEpsilon = 2.220446049250313E-16;
    private const double SingularityEpsilon = MachineEpsilon * 100;

    internal static double[] CreateInverse(LetterboxTransform letterbox, int outputSize)
    {
        var (sourceX, sourceY) = GetCornerPoints(letterbox);
        float size = outputSize;
        float[] destinationX = [0f, 0f, size, size];
        float[] destinationY = [size, 0f, 0f, size];
        var coefficients = new double[UnknownCount, UnknownCount];
        var constants = new double[UnknownCount];
        for (var index = 0; index < 4; index++)
        {
            coefficients[index, 0] = coefficients[index + 4, 3] = sourceX[index];
            coefficients[index, 1] = coefficients[index + 4, 4] = sourceY[index];
            coefficients[index, 2] = coefficients[index + 4, 5] = 1;
            coefficients[index, 6] = -sourceX[index] * destinationX[index];
            coefficients[index, 7] = -sourceY[index] * destinationX[index];
            coefficients[index + 4, 6] = -sourceX[index] * destinationY[index];
            coefficients[index + 4, 7] = -sourceY[index] * destinationY[index];
            constants[index] = destinationX[index];
            constants[index + 4] = destinationY[index];
        }
        SolveInPlace(coefficients, constants);
        double[] forward = [.. constants, 1.0];
        return Invert(forward);
    }

    private static (float[] X, float[] Y) GetCornerPoints(LetterboxTransform letterbox)
    {
        const float HalfCosine = 0.5f;
        const float HalfSine = 0f;
        var centerX = letterbox.CenterX;
        var centerY = letterbox.CenterY;
        var width = letterbox.Width;
        var height = letterbox.Height;
        var x0 = centerX - HalfSine * height - HalfCosine * width;
        var y0 = centerY + HalfCosine * height - HalfSine * width;
        var x1 = centerX + HalfSine * height - HalfCosine * width;
        var y1 = centerY - HalfCosine * height - HalfSine * width;
        return ([x0, x1, 2 * centerX - x0, 2 * centerX - x1], [y0, y1, 2 * centerY - y0, 2 * centerY - y1]);
    }

    private static void SolveInPlace(double[,] matrix, double[] constants)
    {
        var size = constants.Length;
        for (var column = 0; column < size; column++)
        {
            var pivot = column;
            for (var row = column + 1; row < size; row++)
            {
                if (Math.Abs(matrix[row, column]) > Math.Abs(matrix[pivot, column]))
                {
                    pivot = row;
                }
            }
            if (Math.Abs(matrix[pivot, column]) < SingularityEpsilon) throw new InvalidOperationException("The letterbox transform is singular.");
            if (pivot != column)
            {
                for (var index = column; index < size; index++) (matrix[column, index], matrix[pivot, index]) = (matrix[pivot, index], matrix[column, index]);
                (constants[column], constants[pivot]) = (constants[pivot], constants[column]);
            }
            var negativeReciprocal = -1 / matrix[column, column];
            for (var row = column + 1; row < size; row++)
            {
                var factor = matrix[row, column] * negativeReciprocal;
                for (var index = column + 1; index < size; index++) matrix[row, index] += factor * matrix[column, index];
                constants[row] += factor * constants[column];
            }
        }
        for (var row = size - 1; row >= 0; row--)
        {
            var sum = constants[row];
            for (var index = row + 1; index < size; index++) sum -= matrix[row, index] * constants[index];
            constants[row] = sum / matrix[row, row];
        }
    }

    private static double[] Invert(double[] matrix)
    {
        double Get(int row, int column) => matrix[row * 3 + column];
        var determinant = Get(0, 0) * (Get(1, 1) * Get(2, 2) - Get(1, 2) * Get(2, 1)) - Get(0, 1) * (Get(1, 0) * Get(2, 2) - Get(1, 2) * Get(2, 0)) + Get(0, 2) * (Get(1, 0) * Get(2, 1) - Get(1, 1) * Get(2, 0));
        if (determinant == 0) throw new InvalidOperationException("The letterbox transform is not invertible.");
        var scale = 1.0 / determinant;
        return
        [
            (Get(1, 1) * Get(2, 2) - Get(1, 2) * Get(2, 1)) * scale,
            (Get(0, 2) * Get(2, 1) - Get(0, 1) * Get(2, 2)) * scale,
            (Get(0, 1) * Get(1, 2) - Get(0, 2) * Get(1, 1)) * scale,
            (Get(1, 2) * Get(2, 0) - Get(1, 0) * Get(2, 2)) * scale,
            (Get(0, 0) * Get(2, 2) - Get(0, 2) * Get(2, 0)) * scale,
            (Get(0, 2) * Get(1, 0) - Get(0, 0) * Get(1, 2)) * scale,
            (Get(1, 0) * Get(2, 1) - Get(1, 1) * Get(2, 0)) * scale,
            (Get(0, 1) * Get(2, 0) - Get(0, 0) * Get(2, 1)) * scale,
            (Get(0, 0) * Get(1, 1) - Get(0, 1) * Get(1, 0)) * scale,
        ];
    }
}
