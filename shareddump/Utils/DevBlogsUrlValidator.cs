namespace SharedDump.Utils;

public static class DevBlogsUrlValidator
{
    /// <summary>
    /// Returns true if the URL is a valid Microsoft DevBlogs article URL.
    /// </summary>
    public static bool IsValidDevBlogsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!url.StartsWith("https://devblogs.microsoft.com/", StringComparison.OrdinalIgnoreCase))
            return false;
        // Must not end with /feed or /feed/
        if (url.TrimEnd('/').EndsWith("/feed", StringComparison.OrdinalIgnoreCase))
            return false;
        // Should have at least one more path segment after the domain
        var uri = new Uri(url, UriKind.Absolute);
        return uri.Segments.Length > 1;
    }
}
