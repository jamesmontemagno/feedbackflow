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

        if (TryParseScopedDiscussionUrl(segments, out var scopedDiscussionUrlInfo))
        {
            return scopedDiscussionUrlInfo;
        }

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
                        Number = number,
                        DiscussionScope = GitHubDiscussionScope.Repository
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

    private static bool TryParseScopedDiscussionUrl(string[] segments, out GitHubUrlInfo? urlInfo)
    {
        urlInfo = null;

        if (segments.Length < 4 || !segments[2].Equals("discussions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var scope = segments[0].ToLowerInvariant() switch
        {
            "orgs" => GitHubDiscussionScope.Organization,
            "users" => GitHubDiscussionScope.User,
            _ => GitHubDiscussionScope.Repository
        };

        if (scope is GitHubDiscussionScope.Repository || !int.TryParse(segments[3], out var number))
        {
            return false;
        }

        urlInfo = new GitHubUrlInfo
        {
            Owner = segments[1],
            Repository = string.Empty,
            Type = GitHubUrlType.Discussion,
            Number = number,
            DiscussionScope = scope
        };

        return true;
    }
}

public class GitHubUrlInfo
{
    public required string Owner { get; set; }
    public required string Repository { get; set; }
    public required GitHubUrlType Type { get; set; }
    public int? Number { get; set; }
    public GitHubDiscussionScope DiscussionScope { get; set; } = GitHubDiscussionScope.Repository;
}

public enum GitHubUrlType
{
    Repository,
    Issue,
    PullRequest,
    Discussion
}

public enum GitHubDiscussionScope
{
    Repository,
    Organization,
    User
}
