namespace FaceCrop.Sample;

/// <summary>
/// Splits a file name at its last dot. Names that start with their only dot, such as <c>.png</c>, have no extension.
/// </summary>
internal static class FileNameParts
{
    public static string GetSuffix(string name)
    {
        var index = name.LastIndexOf('.');
        return index > 0 && index < name.Length - 1 ? name[index..] : "";
    }

    public static string GetStem(string name)
    {
        var index = name.LastIndexOf('.');
        return index > 0 && index < name.Length - 1 ? name[..index] : name;
    }
}
