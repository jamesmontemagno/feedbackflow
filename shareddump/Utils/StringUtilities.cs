using System.Text.RegularExpressions;

namespace SharedDump.Utils;

public static partial class StringUtilities
{
    public static string Slugify(string? value) =>
        value is null ? "" : SlugRegex().Replace(value, "-").ToLowerInvariant();

    /// <summary>
    /// Gets the first 5 characters of a user ID for display to users for support purposes.
    /// </summary>
    /// <param name="userId">The user ID to get the first 5 digits from</param>
    /// <returns>The first 5 characters of the user ID, or the full ID if it's shorter than 5 characters</returns>
    public static string GetFirst5Digits(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "N/A";
        
        return userId.Length >= 5 ? userId[..5] : userId;
    }

    [GeneratedRegex(@"[^A-Za-z0-9_\.~]+")]
    private static partial Regex SlugRegex();
}