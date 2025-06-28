using System.Text.RegularExpressions;

namespace SharedDump.Services;

public class UrlValidationService
{
    // GitHub username/organization name validation
    private static readonly Regex GitHubNameRegex = new(@"^[a-zA-Z0-9]([a-zA-Z0-9\-])*[a-zA-Z0-9]$|^[a-zA-Z0-9]$", RegexOptions.Compiled);
    // Subreddit name validation (letters, numbers, underscores, 3-21 characters)
    private static readonly Regex SubredditNameRegex = new(@"^[a-zA-Z0-9_]{3,21}$", RegexOptions.Compiled);

    public static UrlValidationResult ValidateGitHubOwnerName(string? ownerName)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub owner name is required." };

        // Check length constraints (GitHub usernames are 1-39 characters)
        if (ownerName.Length > 39)
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub owner name cannot be longer than 39 characters." };

        // Check for valid GitHub username format
        if (!GitHubNameRegex.IsMatch(ownerName))
            return new UrlValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "GitHub owner name can only contain alphanumeric characters and hyphens, and cannot start or end with a hyphen." 
            };

        // Check for consecutive hyphens (not allowed in GitHub usernames)
        if (ownerName.Contains("--"))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub owner name cannot contain consecutive hyphens." };

        return new UrlValidationResult { IsValid = true };
    }

    public static UrlValidationResult ValidateGitHubRepoName(string? repoName)
    {
        if (string.IsNullOrWhiteSpace(repoName))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub repository name is required." };

        // Check length constraints (GitHub repo names are 1-100 characters)
        if (repoName.Length > 100)
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub repository name cannot be longer than 100 characters." };

        // Repository names are more flexible than usernames
        // They can contain letters, numbers, hyphens, underscores, and periods
        if (!Regex.IsMatch(repoName, @"^[a-zA-Z0-9._-]+$"))
            return new UrlValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "GitHub repository name can only contain letters, numbers, hyphens, underscores, and periods." 
            };

        // Cannot start with a period
        if (repoName.StartsWith('.'))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "GitHub repository name cannot start with a period." };

        return new UrlValidationResult { IsValid = true };
    }

    public static UrlValidationResult ValidateSubredditName(string? subredditName)
    {
        if (string.IsNullOrWhiteSpace(subredditName))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "Subreddit name is required." };

        // Remove r/ prefix if present
        var cleanName = subredditName.TrimStart('r', '/');

        // Check basic format (3-21 characters, letters, numbers, underscores only)
        if (!SubredditNameRegex.IsMatch(cleanName))
            return new UrlValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Subreddit name must be 3-21 characters and contain only letters, numbers, and underscores." 
            };

        // Check for common reserved names
        var reservedNames = new[] { "www", "mail", "admin", "api", "blog", "about", "help", "reddit" };
        if (reservedNames.Contains(cleanName.ToLowerInvariant()))
            return new UrlValidationResult { IsValid = false, ErrorMessage = "This subreddit name is reserved and not allowed." };

        return new UrlValidationResult { IsValid = true };
    }

    // Helper methods to construct URLs from validated components
    public static string ConstructGitHubUrl(string owner, string repo)
    {
        return $"https://github.com/{owner}/{repo}";
    }

    public static string ConstructRedditUrl(string subreddit)
    {
        return $"https://www.reddit.com/r/{subreddit}/";
    }
}

public class UrlValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}