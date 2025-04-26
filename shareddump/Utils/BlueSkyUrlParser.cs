using System.Text.RegularExpressions;

namespace SharedDump.Utils;

/// <summary>
/// Utility class for parsing BlueSky URLs and IDs
/// </summary>
public static class BlueSkyUrlParser
{
    // Regular expression for BlueSky URLs:
    // - Full URL format: https://bsky.app/profile/username.bsky.social/post/3jxeu2oa2rp2m
    // - Short format: username.bsky.social/3jxeu2oa2rp2m
    private static readonly Regex _blueSkyUrlRegex = new(
        @"(?:https?://bsky\.app/profile/)?([^/]+)/post/([a-zA-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    /// <summary>
    /// Extracts the post ID from a BlueSky URL or ID
    /// </summary>
    /// <param name="input">The BlueSky URL or ID</param>
    /// <returns>The extracted post ID, or null if not a valid BlueSky URL or ID</returns>
    public static string? ExtractPostId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        
        // If it looks like a direct post ID (no URL structure), return as is
        if (Regex.IsMatch(input, @"^[a-zA-Z0-9]+$") && input.Length > 10)
            return input;
        
        // Try matching BlueSky URL patterns
        var match = _blueSkyUrlRegex.Match(input);
        if (match.Success)
        {
            var username = match.Groups[1].Value;
            var postId = match.Groups[2].Value;
            
            // Return in the format used by the BlueSky API: username/postId
            return $"{username}/post/{postId}";
        }
        
        // Special case: handle format like "username.bsky.social/postid" without "/post/" part
        var parts = input.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0].Contains(".bsky.") && parts[1].Length > 10)
        {
            // Convert to proper format
            return $"{parts[0]}/post/{parts[1]}";
        }
        
        return null;
    }
    
    /// <summary>
    /// Determines whether a string is a valid BlueSky URL or ID
    /// </summary>
    /// <param name="input">The string to check</param>
    /// <returns>True if the string is a valid BlueSky URL or ID, false otherwise</returns>
    public static bool IsValidBlueSkyUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        return ExtractPostId(input) != null;
    }
}
