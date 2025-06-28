using SharedDump.Utils;

namespace SharedDump.Services;

public class UrlValidationService
{
    public static UrlValidationResult ValidateGitHubUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub URL is required." };

        // First check if it's a valid URL format
        if (!UrlParsing.IsValidUrl(url) || !UrlParsing.IsGitHubUrl(url))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "Please enter a valid GitHub URL." };

        // Try to parse the URL to ensure it's a supported GitHub URL type
        var parsedUrl = GitHubUrlParser.ParseUrl(url);
        if (parsedUrl == null)
        {
            return new UrlValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "GitHub URL must be a repository, issue, pull request, or discussion URL (e.g., https://github.com/owner/repo/issues/123)." 
            };
        }

        var (owner, repo, type, number) = parsedUrl.Value;
        
        // Validate owner and repo names
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub URL must contain valid owner and repository names." };

        return new UrlValidationResult 
        { 
            IsValid = true, 
            ParsedData = new GitHubUrlData { Owner = owner, Repository = repo, Type = type, Number = number }
        };
    }

    public static UrlValidationResult ValidateRedditUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "Reddit URL is required." };

        // First check if it's a valid URL format
        if (!UrlParsing.IsValidUrl(url) || !UrlParsing.IsRedditUrl(url))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "Please enter a valid Reddit URL." };

        // Try to parse the Reddit URL
        var threadId = RedditUrlParser.ParseUrl(url);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return new UrlValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Reddit URL must be a valid post or comment URL (e.g., https://www.reddit.com/r/subreddit/comments/abc123/title/)." 
            };
        }

        // Extract subreddit from URL
        var subreddit = ExtractSubredditFromUrl(url);
        if (string.IsNullOrWhiteSpace(subreddit))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "Unable to extract subreddit from Reddit URL." };

        return new UrlValidationResult 
        { 
            IsValid = true, 
            ParsedData = new RedditUrlData { Subreddit = subreddit, ThreadId = threadId }
        };
    }

    private static string? ExtractSubredditFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.Segments;
            
            // Look for /r/subreddit pattern
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].Equals("r/", StringComparison.OrdinalIgnoreCase))
                {
                    return segments[i + 1].TrimEnd('/');
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return null;
    }
}

public class UrlValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public object? ParsedData { get; set; }
}

public class GitHubUrlData
{
    public required string Owner { get; set; }
    public required string Repository { get; set; }
    public required string Type { get; set; }
    public required int Number { get; set; }
}

public class RedditUrlData
{
    public required string Subreddit { get; set; }
    public required string ThreadId { get; set; }
}