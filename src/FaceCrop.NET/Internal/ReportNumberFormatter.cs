using System.Globalization;

namespace FaceCrop.Internal;

/// <summary>
/// Formats doubles with the shortest digits that round-trip. Whole numbers keep a trailing <c>.0</c>,
/// and exponent notation (<c>1e-05</c>, <c>1e+16</c>) is used below 1e-4 and from 1e16.
/// </summary>
internal static class ReportNumberFormatter
{
    internal static string Format(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        if (value == 0) return double.IsNegative(value) ? "-0.0" : "0.0";
        var (digits, decimalPoint) = GetShortestDigits(Math.Abs(value));
        var sign = value < 0 ? "-" : "";
        if (decimalPoint <= -4 || decimalPoint > 16)
        {
            var exponent = decimalPoint - 1;
            var mantissa = digits.Length > 1 ? $"{digits[0]}.{digits[1..]}" : digits;
            return string.Create(CultureInfo.InvariantCulture, $"{sign}{mantissa}e{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent):00}");
        }
        if (decimalPoint <= 0) return $"{sign}0.{new string('0', -decimalPoint)}{digits}";
        if (decimalPoint >= digits.Length) return $"{sign}{digits}{new string('0', decimalPoint - digits.Length)}.0";
        return $"{sign}{digits[..decimalPoint]}.{digits[decimalPoint..]}";
    }

    // Returns significant digits without leading or trailing zeros and the position of the decimal point relative to them.
    private static (string Digits, int DecimalPoint) GetShortestDigits(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponent = 0;
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        if (exponentIndex >= 0)
        {
            exponent = int.Parse(text[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text[..exponentIndex];
        }
        var pointIndex = text.IndexOf('.');
        var digits = pointIndex >= 0 ? string.Concat(text.AsSpan(0, pointIndex), text.AsSpan(pointIndex + 1)) : text;
        var decimalPoint = (pointIndex >= 0 ? pointIndex : text.Length) + exponent;
        var leadingZeros = 0;
        while (leadingZeros < digits.Length - 1 && digits[leadingZeros] == '0') leadingZeros++;
        return (digits[leadingZeros..].TrimEnd('0'), decimalPoint - leadingZeros);
    }
}
