using System.Text.RegularExpressions;

namespace FeedbackWebApp.Components.Feedback;

public static class GitHubUrlParser
{
    private static readonly Regex IssueRegex = new(@"github\.com/([^/]+)/([^/]+)/issues/(\d+)", RegexOptions.Compiled);
    private static readonly Regex PullRequestRegex = new(@"github\.com/([^/]+)/([^/]+)/pull/(\d+)", RegexOptions.Compiled);
    private static readonly Regex DiscussionRegex = new(@"github\.com/([^/]+)/([^/]+)/discussions/(\d+)", RegexOptions.Compiled);

    public static (string owner, string repo, string type, int number)? ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Try to match issue URL
        var match = IssueRegex.Match(url);
        if (match.Success)
        {
            return (
                match.Groups[1].Value,
                match.Groups[2].Value,
                "issue",
                int.Parse(match.Groups[3].Value)
            );
        }

        // Try to match pull request URL
        match = PullRequestRegex.Match(url);
        if (match.Success)
        {
            return (
                match.Groups[1].Value,
                match.Groups[2].Value,
                "pull",
                int.Parse(match.Groups[3].Value)
            );
        }

        // Try to match discussion URL
        match = DiscussionRegex.Match(url);
        if (match.Success)
        {
            return (
                match.Groups[1].Value,
                match.Groups[2].Value,
                "discussion",
                int.Parse(match.Groups[3].Value)
            );
        }

        return null;
    }
}