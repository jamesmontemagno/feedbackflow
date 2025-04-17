using System.Text.RegularExpressions;

namespace SharedDump.Utils;

public static class RedditUrlParser
{
    private static readonly Regex ThreadRegex = new(
        @"(?:https?://)?(?:www\.)?reddit\.com/r/[^/]+/comments/([a-z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = ThreadRegex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static IEnumerable<string> ParseMultipleIds(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        // Split by commas and process each part
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            
            // Check if it's a URL
            var threadId = ParseUrl(trimmed);
            if (threadId != null)
            {
                yield return threadId;
                continue;
            }

            // If it's not a URL, check if it's a direct thread ID (alphanumeric)
            if (Regex.IsMatch(trimmed, "^[a-z0-9]+$", RegexOptions.IgnoreCase))
            {
                yield return trimmed;
            }
        }
    }
}