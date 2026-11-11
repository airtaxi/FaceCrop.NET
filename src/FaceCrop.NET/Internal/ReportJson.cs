using System.Collections;
using System.Globalization;
using System.Text;

namespace FaceCrop.Internal;

/// <summary>
/// Writes report JSON with <c>", "</c> and <c>": "</c> separators, shortest round-trip floats and lowercase <c>\uXXXX</c> escapes.
/// Objects are ordered lists of members so that key order is preserved.
/// </summary>
internal static class ReportJson
{
    internal static IReadOnlyList<KeyValuePair<string, object?>> Object(params (string Name, object? Value)[] members) =>
    [.. members.Select(member => new KeyValuePair<string, object?>(member.Name, member.Value))];

    internal static string Serialize(object? value, bool escapeNonAscii)
    {
        var builder = new StringBuilder();
        Write(builder, value, escapeNonAscii);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, object? value, bool escapeNonAscii)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case string text:
                WriteString(builder, text, escapeNonAscii);
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case int number:
                builder.Append(number.ToString(CultureInfo.InvariantCulture));
                break;
            case double number:
                builder.Append(ReportNumberFormatter.Format(number));
                break;
            case IEnumerable<KeyValuePair<string, object?>> members:
                builder.Append('{');
                var firstMember = true;
                foreach (var (name, memberValue) in members)
                {
                    if (!firstMember) builder.Append(", ");
                    firstMember = false;
                    WriteString(builder, name, escapeNonAscii);
                    builder.Append(": ");
                    Write(builder, memberValue, escapeNonAscii);
                }
                builder.Append('}');
                break;
            case IEnumerable items:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in items)
                {
                    if (!firstItem) builder.Append(", ");
                    firstItem = false;
                    Write(builder, item, escapeNonAscii);
                }
                builder.Append(']');
                break;
            default:
                throw new NotSupportedException($"Values of type {value.GetType()} cannot be written to the report.");
        }
    }

    private static void WriteString(StringBuilder builder, string text, bool escapeNonAscii)
    {
        builder.Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (character < ' ' || (escapeNonAscii && character > '~')) builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
                    else builder.Append(character);
                    break;
            }
        }
        builder.Append('"');
    }
}
