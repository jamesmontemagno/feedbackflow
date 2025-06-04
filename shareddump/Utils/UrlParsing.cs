namespace SharedDump.Utils;

public static class UrlParsing
{
    public static string? ExtractVideoId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim();
        
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("youtube.com") && uri.Query.Contains("v="))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"];
            }
            else if (uri.Host.Contains("youtube.com") && uri.AbsolutePath.StartsWith("/live/"))
            {
                return uri.AbsolutePath.Substring("/live/".Length);
            }
            else if (uri.Host == "youtu.be")
            {
                return uri.AbsolutePath.Trim('/');
            }
        }
        
        // If not a URL, return as raw ID
        return input;
    }

    public static string? ExtractPlaylistId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim();
        
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("youtube.com"))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["list"];
            }
        }
        
        // If not a URL, return as raw ID
        return input;
    }

    public static string? ExtractHackerNewsId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        input = input.Trim();
        
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("ycombinator.com") && uri.AbsolutePath.Contains("/item"))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["id"];
            }
        }
        
        // Only return as raw ID if it's a number
        return long.TryParse(input, out _) ? input : null;
    }

    public static string? ExtractRedditId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Handle direct thread IDs
        if (url.StartsWith("t3_"))
            return url[3..];

        // Handle full Reddit URLs
        if (TryParseRedditUrl(url, out var threadId))
            return threadId;
            
        if (IsValidRedditId(url))
            return url;
            
        if (RedditUrlParser.IsRedditShortUrl(url))
            return url;

        return null;
    }

    private static bool TryParseRedditUrl(string url, out string threadId)
    {
        threadId = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var uri = new Uri(url);
            if (!uri.Host.Contains("reddit.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var segments = uri.Segments;
            var commentsIndex = Array.IndexOf(segments, "comments/");
            if (commentsIndex == -1 || commentsIndex + 1 >= segments.Length)
                return false;

            threadId = segments[commentsIndex + 1].Trim('/');
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidRedditId(string id)
    {
        // Reddit IDs are 5-7 characters of base36 (alphanumeric)
        return !string.IsNullOrWhiteSpace(id) && 
               id.Length >= 5 && 
               id.Length <= 7 && 
               id.All(c => char.IsLetterOrDigit(c));
    }

    public static bool IsYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGitHubUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRedditUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("reddit.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDevBlogsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("devblogs.microsoft.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTwitterUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host.Equals("twitter.com") || host.Equals("x.com");
    }

    public static bool IsBlueSkyUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return BlueSkyUrlParser.IsValidBlueSkyUrl(url);
    }

    public static bool IsHackerNewsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("ycombinator.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        return Uri.TryCreate(input, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
    
    public static string GetYouTubeThumbnailUrl(string videoId)
    {
        return $"https://i.ytimg.com/vi/{videoId}/mqdefault.jpg";
    }
}