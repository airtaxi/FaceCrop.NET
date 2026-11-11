using System.Buffers;
using System.Text;

namespace FaceCrop.Sample;

/// <summary>
/// Writes a UTF-8 (with BOM) CSV file with CRLF line endings, quoting only fields that contain commas, quotes or line breaks.
/// Each row is flushed immediately so the report stays complete if the run is interrupted.
/// </summary>
/// <param name="path">The file to create or replace.</param>
internal sealed class CsvReportWriter(string path) : IDisposable
{
    private static readonly SearchValues<char> s_specialCharacters = SearchValues.Create(",\"\r\n");
    private readonly StreamWriter _writer = new(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    public void WriteRow(IReadOnlyList<string> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0) _writer.Write(',');
            _writer.Write(Quote(fields[index]));
        }
        _writer.Write("\r\n");
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();

    private static string Quote(string field) => field.AsSpan().IndexOfAny(s_specialCharacters) < 0 ? field : $"\"{field.Replace("\"", "\"\"")}\"";
}
