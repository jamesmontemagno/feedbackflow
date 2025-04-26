namespace SharedDump.Utils;

/// <summary>
/// Utility for parsing Twitter/X URLs and extracting tweet IDs.
/// </summary>
public static class TwitterUrlParser
{
    /// <summary>
    /// Extracts the tweet ID from a Twitter/X URL or returns the ID if already provided.
    /// </summary>
    /// <param name="tweetUrlOrId">A tweet URL or tweet ID.</param>
    /// <returns>The tweet ID if found, otherwise null.</returns>    
    public static string? ExtractTweetId(string tweetUrlOrId)
    {
        if (string.IsNullOrWhiteSpace(tweetUrlOrId)) return null;

        // If it's just a numeric ID, validate it's a proper tweet ID (13-19 digits)
        if (tweetUrlOrId.All(char.IsDigit))
        {
            return IsTweetIdValid(tweetUrlOrId) ? tweetUrlOrId : null;
        }

        try
        {
            // Try to parse as URL
            if (!Uri.TryCreate(tweetUrlOrId, UriKind.Absolute, out var uri))
            {
                // Try adding https:// if not present
                if (!tweetUrlOrId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    tweetUrlOrId = "https://" + tweetUrlOrId;
                    if (!Uri.TryCreate(tweetUrlOrId, UriKind.Absolute, out uri))
                        return null;
                }
                else return null;
            }

            // Validate it's a Twitter/X domain
            var host = uri.Host.ToLowerInvariant();
            if (!host.Equals("twitter.com") && !host.Equals("x.com"))
                return null;

            // Parse the path segments
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3) return null;

            // Check for standard tweet URL format
            if (segments.Length >= 3 && segments[1].Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                var potentialId = segments[2];
                return IsTweetIdValid(potentialId) ? potentialId : null;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsTweetIdValid(string id)
    {
        // Tweet IDs are snowflakes, which are 13-19 digits long
        if (id.Length < 13 || id.Length > 19) return false;
        return long.TryParse(id, out _);
    }
}
