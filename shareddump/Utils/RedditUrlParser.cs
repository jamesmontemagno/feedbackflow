using System.Text.RegularExpressions;

namespace SharedDump.Utils;

public static class RedditUrlParser
{
    private static readonly Regex ThreadRegex = new(
        @"(?:https?://)?(?:www\.)?reddit\.com/r/[^/]+/comments/([a-z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortlinkRegex = new(
        @"(?:https?://)?(?:www\.)?reddit\.com/r/[^/]+/s/([a-zA-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);



    public static string? ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = ThreadRegex.Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    public static bool IsRedditShortUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) && ShortlinkRegex.IsMatch(url);
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

    /// <summary>
    /// Generates a properly formatted Reddit comment URL
    /// </summary>
    /// <param name="threadPermalink">The thread permalink (e.g., "/r/subreddit/comments/threadid/title/")</param>
    /// <param name="commentId">The comment ID</param>
    /// <returns>A properly formatted Reddit comment URL</returns>
    public static string GenerateCommentUrl(string threadPermalink, string commentId)
    {
        if (string.IsNullOrWhiteSpace(threadPermalink) || string.IsNullOrWhiteSpace(commentId))
            return string.Empty;

        // Ensure permalink starts with /
        if (!threadPermalink.StartsWith('/'))
        {
            threadPermalink = "/" + threadPermalink;
        }
        
        // Ensure permalink ends with /
        if (!threadPermalink.EndsWith('/'))
        {
            threadPermalink += "/";
        }
        
        // Construct the full URL
        return $"https://www.reddit.com{threadPermalink}{commentId}/";
    }
}
