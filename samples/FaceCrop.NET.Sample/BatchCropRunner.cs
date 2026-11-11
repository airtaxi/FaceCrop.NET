using System.Globalization;
using System.Text;

namespace FaceCrop.Sample;

/// <summary>
/// Crops every supported image in a directory and writes <c>crop_report.csv</c> next to the results.
/// </summary>
internal static class BatchCropRunner
{
    private const string ReportFileName = "crop_report.csv";
    private const string OutputSuffix = "_crop";
    private static readonly HashSet<string> s_extensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff"];
    private static readonly string[] s_reportHeader = ["input", "output", "status", "detected_faces", "crop_box", "detail", "excluded_right_px", "detections_json"];
    private static readonly StringComparison s_pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static int Run(BatchCropSettings settings)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.InputDirectory));
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.OutputDirectory ?? Path.Combine(root, "cropped")));
        if (string.Equals(root, output, s_pathComparison))
        {
            Console.Error.WriteLine("error: Use a separate output directory.");
            return 2;
        }
        Directory.CreateDirectory(output);
        using var cropper = new FaceCropper(settings.Options);
        using var report = new CsvReportWriter(Path.Combine(output, ReportFileName));
        report.WriteRow(s_reportHeader);
        var counts = new OrderedDictionary<string, int>();
        foreach (var source in SourceFileEnumerator.Enumerate(root, settings.Recursive))
        {
            var name = Path.GetFileName(source);
            if (!s_extensions.Contains(FileNameParts.GetSuffix(name).ToLowerInvariant())) continue;
            if (IsInside(source, output) || FileNameParts.GetStem(name).ToLowerInvariant().EndsWith(OutputSuffix, StringComparison.Ordinal)) continue;
            var destination = GetDestination(root, output, source);
            var result = Path.Exists(destination) && !settings.Overwrite ? null : cropper.CropFile(source, destination);
            var status = result?.Status.Code ?? "exists";
            var detail = result?.Detail ?? "";
            counts[status] = counts.GetValueOrDefault(status) + 1;
            report.WriteRow([source, destination, status, (result?.Faces.Count ?? 0).ToString(CultureInfo.InvariantCulture), result?.CropBox?.ToString() ?? "", detail, (result?.ExcludedRightPixels ?? 0).ToString(CultureInfo.InvariantCulture), result?.GetDiagnosticsJson() ?? "[]"]);
            Console.WriteLine($"[{status}] {name} {detail}");
        }
        Console.WriteLine($"Summary: {FormatCounts(counts)}");
        return counts.ContainsKey(FaceCropStatus.Error.Code) ? 1 : 0;
    }

    private static bool IsInside(string path, string directory) => path.StartsWith(directory + Path.DirectorySeparatorChar, s_pathComparison);

    private static string GetDestination(string root, string output, string source)
    {
        var relativeDirectory = Path.GetRelativePath(root, Path.GetDirectoryName(source)!);
        var fileName = FileNameParts.GetStem(Path.GetFileName(source)) + OutputSuffix + ".webp";
        return relativeDirectory == "." ? Path.Combine(output, fileName) : Path.Combine(output, relativeDirectory, fileName);
    }

    private static string FormatCounts(OrderedDictionary<string, int> counts)
    {
        var builder = new StringBuilder("{");
        foreach (var (status, count) in counts)
        {
            if (builder.Length > 1) builder.Append(", ");
            builder.Append(CultureInfo.InvariantCulture, $"'{status}': {count}");
        }
        return builder.Append('}').ToString();
    }
}
