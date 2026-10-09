using System.Text.RegularExpressions;

namespace GameTracker.Infrastructure.Detection.Libraries;

/// <summary>Minimal reader for Valve's KeyValues (.vdf/.acf) text format: enough for flat fields.</summary>
internal static partial class VdfReader
{
    /// <summary>First value of every key, in document order (top-level keys come first in manifests).</summary>
    public static Dictionary<string, string> ReadFirstValues(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeyValuePattern().Matches(content))
            values.TryAdd(match.Groups["key"].Value, Unescape(match.Groups["value"].Value));
        return values;
    }

    public static IEnumerable<string> ReadAllValues(string content, string key) =>
        KeyValuePattern().Matches(content)
            .Where(m => string.Equals(m.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase))
            .Select(m => Unescape(m.Groups["value"].Value));

    private static string Unescape(string value) => value.Replace(@"\\", @"\", StringComparison.Ordinal);

    [GeneratedRegex("""
        "(?<key>[^"]+)"\s+"(?<value>(?:[^"\\]|\\.)*)"
        """)]
    private static partial Regex KeyValuePattern();
}
