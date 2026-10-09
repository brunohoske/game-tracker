namespace GameTracker.Core.Common;

/// <summary>Windows path helpers used for case-insensitive matching of executables and folders.</summary>
public static class PathUtil
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>Canonical, case-insensitive form of a path: backslashes, no trailing separator, lower case.</summary>
    public static string Normalize(string path) =>
        path.Trim().Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();

    /// <summary>True when <paramref name="normalizedPath"/> is inside <paramref name="normalizedDirectory"/>.</summary>
    public static bool IsUnder(string normalizedPath, string normalizedDirectory) =>
        normalizedPath.Length > normalizedDirectory.Length
        && normalizedPath[normalizedDirectory.Length] == '\\'
        && normalizedPath.StartsWith(normalizedDirectory, StringComparison.Ordinal);

    public static string FileNameWithoutExtension(string path)
    {
        var name = path[(path.LastIndexOfAny(Separators) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    public static string? DirectoryOf(string path)
    {
        var index = path.LastIndexOfAny(Separators);
        return index > 0 ? path[..index] : null;
    }
}
