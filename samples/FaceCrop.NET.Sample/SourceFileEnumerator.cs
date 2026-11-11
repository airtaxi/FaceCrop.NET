namespace FaceCrop.Sample;

/// <summary>
/// Lists files directory by directory. Each directory is listed once, in file system order.
/// In recursive mode the root comes first, followed by subdirectories in the order a top-down walk discovers them;
/// directory links are not followed.
/// </summary>
internal static class SourceFileEnumerator
{
    public static IEnumerable<string> Enumerate(string root, bool recursive)
    {
        var directories = recursive ? EnumerateDirectories(root) : [root];
        foreach (var directory in directories)
        {
            List<string> files;
            try { files = [.. Directory.EnumerateFiles(directory)]; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }
            foreach (var file in files) yield return file;
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string root)
    {
        yield return root;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            List<string> children;
            try { children = [.. new DirectoryInfo(pending.Pop()).EnumerateDirectories().Where(child => !child.Attributes.HasFlag(FileAttributes.ReparsePoint)).Select(child => child.FullName)]; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }
            foreach (var child in children) yield return child;
            for (var index = children.Count - 1; index >= 0; index--) pending.Push(children[index]);
        }
    }
}
