using System.Globalization;

namespace FaceCrop.Imaging;

/// <summary>
/// An opaque 8-bit RGB color.
/// </summary>
/// <param name="Red">Red channel.</param>
/// <param name="Green">Green channel.</param>
/// <param name="Blue">Blue channel.</param>
public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    /// <summary>
    /// Pure white.
    /// </summary>
    public static RgbColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>
    /// Parses a color written as <c>#RRGGBB</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="color">The parsed color when parsing succeeds.</param>
    public static bool TryParseHex(string? text, out RgbColor color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#') return false;
        if (!int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)) return false;
        color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }
}
