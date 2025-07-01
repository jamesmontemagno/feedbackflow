using System.Text.RegularExpressions;

namespace SharedDump.Utils;

public static class GitHubUrlParser
{
    public static GitHubUrlInfo? ParseGitHubUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return null;

        var owner = segments[0];
        var repository = segments[1];

        // Handle different GitHub URL patterns
        if (segments.Length >= 4)
        {
            var type = segments[2]; // "issues", "pull", "discussions"
            var numberString = segments[3];

            if (int.TryParse(numberString, out var number))
            {
                return type.ToLowerInvariant() switch
                {
                    "issues" => new GitHubUrlInfo
                    {
                        Owner = owner,
                        Repository = repository,
                        Type = GitHubUrlType.Issue,
                        Number = number
                    },
                    "pull" => new GitHubUrlInfo
                    {
                        Owner = owner,
                        Repository = repository,
                        Type = GitHubUrlType.PullRequest,
                        Number = number
                    },
                    "discussions" => new GitHubUrlInfo
                    {
                        Owner = owner,
                        Repository = repository,
                        Type = GitHubUrlType.Discussion,
                        Number = number
                    },
                    _ => null
                };
            }
        }

        // If no specific type found, return repository info
        return new GitHubUrlInfo
        {
            Owner = owner,
            Repository = repository,
            Type = GitHubUrlType.Repository,
            Number = null
        };
    }

    public static bool IsGitHubUrl(string url)
    {
        return ParseGitHubUrl(url) != null;
    }
}

public class GitHubUrlInfo
{
    public required string Owner { get; set; }
    public required string Repository { get; set; }
    public required GitHubUrlType Type { get; set; }
    public int? Number { get; set; }
}

public enum GitHubUrlType
{
    Repository,
    Issue,
    PullRequest,
    Discussion
}
