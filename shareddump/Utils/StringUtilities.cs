using System.Text.RegularExpressions;

namespace SharedDump.Utils;

public static partial class StringUtilities
{
    public static string Slugify(string? value) =>
        value is null ? "" : SlugRegex().Replace(value, "-").ToLowerInvariant();

    [GeneratedRegex(@"[^A-Za-z0-9_\.~]+")]
    private static partial Regex SlugRegex();
}