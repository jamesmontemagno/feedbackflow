using System.Text;
using Markdig;
using SharedDump.Models.GitHub;

namespace SharedDump.Utils;

public record TopGitHubIssueInfo(GithubIssueDetail Issue, int Rank, int TotalScore);

public static class GitHubIssuesUtils
{
    private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static string ConvertMarkdownToHtml(string markdown)
    {
        return Markdown.ToHtml(markdown, _markdownPipeline);
    }

    /// <summary>
    /// Ranks GitHub issues by combined comments and reactions score
    /// </summary>
    /// <param name="issues">List of GitHub issue summaries</param>
    /// <param name="topCount">Number of top issues to return</param>
    /// <returns>Top ranked issues</returns>
    public static List<GithubIssueSummary> RankIssuesByEngagement(List<GithubIssueSummary> issues, int topCount = 5)
    {
        return issues
            .OrderByDescending(i => (i.CommentsCount * 0.7) + (i.ReactionsCount * 0.3))
            .Take(topCount)
            .ToList();
    }

    /// <summary>
    /// Gets the oldest issues that are still being actively discussed (high engagement)
    /// </summary>
    /// <param name="issues">List of GitHub issue summaries</param>
    /// <param name="topCount">Number of oldest important issues to return</param>
    /// <returns>Oldest issues with high engagement</returns>
    public static List<GithubIssueSummary> GetOldestImportantIssues(List<GithubIssueSummary> issues, int topCount = 3)
    {
        // Only consider open issues that have decent engagement (at least 2 comments or 1 reaction)
        var minEngagement = 2;
        
        return issues
            .Where(i => i.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase))
            .Where(i => (i.CommentsCount + i.ReactionsCount) >= minEngagement)
            .OrderBy(i => i.CreatedAt) // Oldest first
            .Take(topCount)
            .ToList();
    }

    /// <summary>
    /// Analyzes issue titles to identify common keywords and trends
    /// </summary>
    /// <param name="issues">List of GitHub issues</param>
    /// <returns>Analysis summary of common themes</returns>
    public static string AnalyzeTitleTrends(List<GithubIssueSummary> issues)
    {
        if (!issues.Any())
            return "No issues found in the specified time period.";

        var allTitles = string.Join(" ", issues.Select(i => i.Title.ToLowerInvariant()));
        var words = allTitles.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !CommonStopWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        var openIssues = issues.Count(i => i.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase));
        var closedIssues = issues.Count(i => i.State.Equals("CLOSED", StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine($"📊 **Issue Summary:** {issues.Count} total issues ({openIssues} open, {closedIssues} closed)");
        sb.AppendLine();
        sb.AppendLine($"🔤 **Top Keywords:** {string.Join(", ", words)}");
        
        var topLabels = issues
            .SelectMany(i => i.Labels)
            .Where(l => !string.IsNullOrEmpty(l))
            .GroupBy(l => l)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        if (topLabels.Any())
        {
            sb.AppendLine($"🏷️ **Common Labels:** {string.Join(", ", topLabels)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates HTML email report for GitHub issues
    /// </summary>
    public static string GenerateGitHubIssuesReportEmail(
        string repoName,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string overallAnalysis,
        List<TopGitHubIssueInfo> topIssues,
        List<GithubIssueSummary> oldestImportantIssues,
        string reportId)
    {
        var emailBuilder = new StringBuilder();
        emailBuilder.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>GitHub Issues Report - " + repoName + @"</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #24292e 0%, #586069 100%); color: white; padding: 20px; border-radius: 5px; margin-bottom: 20px; }
        .section { margin-bottom: 30px; }
        .issue { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 15px; border-left: 4px solid #28a745; }
        .issue.closed { border-left-color: #dc3545; }
        .issue-title { color: #0366d6; text-decoration: none; font-weight: bold; font-size: 1.1em; }
        .issue-stats { color: #7c7c7c; font-size: 0.9em; margin: 5px 0; }
        .issue-state { padding: 2px 8px; border-radius: 12px; font-size: 0.8em; font-weight: bold; }
        .state-open { background-color: #28a745; color: white; }
        .state-closed { background-color: #dc3545; color: white; }
        .analysis { background-color: #fff; padding: 15px; border-left: 4px solid #0366d6; margin: 10px 0; }
        .overall-analysis { background-color: #f0f8ff; padding: 20px; border-radius: 5px; margin: 20px 0; }
        .top-issues-list { background-color: #fff3e0; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .divider { border-top: 1px solid #eee; margin: 20px 0; }
        .feedback-button { 
            display: inline-block; 
            background-color: #0366d6; 
            color: white; 
            padding: 8px 16px; 
            border-radius: 4px; 
            text-decoration: none; 
            margin-top: 10px;
            font-size: 0.9em;
        }
        .feedback-button:hover { background-color: #0255b3; }
        .action-buttons { display: flex; gap: 10px; margin-top: 10px; }
        .labels { margin-top: 8px; }
        .label { background-color: #0366d6; color: white; padding: 2px 6px; border-radius: 3px; font-size: 0.8em; margin-right: 4px; }
    </style>
</head>
<body>");

        // Header
        emailBuilder.AppendFormat(@"    <div class='header'>
        <h1>📊 GitHub Issues Report</h1>
        <h2><a href='https://github.com/{0}' style='color: white; text-decoration: none;'>{0}</a></h2>
        <p>Analysis for {1:MMMM dd, yyyy} - {2:MMMM dd, yyyy}</p>
        <a href='https://www.feedbackflow.app/report/{3}' class='feedback-button' style='background-color: white; color: #0366d6;'>Share Report</a>
    </div>", repoName, startDate, endDate, reportId);

        // Top Issues Quick Links
        emailBuilder.AppendLine(@"
    <div class='top-issues-list'>
        <h2>🔝 Top Issues This Week</h2>
        <ul>");
        foreach (var issueInfo in topIssues)
        {
            var issue = issueInfo.Issue.Summary;
            emailBuilder.AppendFormat(@"
            <li><a href='{0}'>#{1} {2}</a> ({3} comments, {4} reactions) 
                <span class='issue-state state-{5}'>{5}</span>
            </li>",
                issue.Url,
                ExtractIssueNumber(issue.Url),
                issue.Title,
                issue.CommentsCount,
                issue.ReactionsCount,
                issue.State.ToLowerInvariant());
        }
        emailBuilder.AppendLine(@"
        </ul>
    </div>");

        // Overall Analysis
        emailBuilder.AppendFormat(@"
    <div class='overall-analysis'>
        <h2>📈 Weekly Summary</h2>
        <div>{0}</div>
    </div>", ConvertMarkdownToHtml(overallAnalysis));

        // Top Issues Details
        emailBuilder.AppendLine(@"
    <div class='section'>
        <h2>🎯 Top Issues Analysis</h2>");

        foreach (var issueInfo in topIssues)
        {
            var issue = issueInfo.Issue.Summary;
            var analysis = issueInfo.Issue.DeepAnalysis;
            
            var stateClass = issue.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase) ? "open" : "closed";
            
            emailBuilder.AppendFormat(@"
        <div class='issue {0}'>
            <h3><a href='{1}' class='issue-title'>#{2} {3}</a></h3>
            <div class='issue-stats'>
                <span class='issue-state state-{4}'>{4}</span> • 
                By <strong>{5}</strong> • 
                {6} comments • {7} reactions • 
                Created {8:MMM dd, yyyy}
            </div>",
                stateClass,
                issue.Url,
                ExtractIssueNumber(issue.Url),
                issue.Title,
                issue.State.ToLowerInvariant(),
                issue.Author,
                issue.CommentsCount,
                issue.ReactionsCount,
                issue.CreatedAt);

            // Labels
            if (issue.Labels.Any())
            {
                emailBuilder.AppendLine(@"
            <div class='labels'>");
                foreach (var label in issue.Labels)
                {
                    emailBuilder.AppendFormat(@"<span class='label'>{0}</span>", label);
                }
                emailBuilder.AppendLine(@"
            </div>");
            }

            emailBuilder.AppendFormat(@"
            <div class='action-buttons'>
                <a href='{0}' class='feedback-button'>View on GitHub</a>
                <a href='https://www.feedbackflow.app/?source=auto&id={1}' class='feedback-button'>Open in FeedbackFlow</a>
            </div>
            <div class='analysis'>
                {2}
            </div>
        </div>", issue.Url, reportId, ConvertMarkdownToHtml(analysis));
        }

        emailBuilder.AppendLine(@"
    </div>");

        // Oldest Important Issues Section
        if (oldestImportantIssues.Any())
        {
            emailBuilder.AppendLine(@"
    <div class='section'>
        <h2>⏰ Oldest Important Issues Still Being Discussed</h2>
        <p style='color: #7c7c7c; margin-bottom: 15px;'>These are the oldest open issues that are still receiving attention from the community:</p>");

            foreach (var issue in oldestImportantIssues)
            {
                var daysSinceCreated = (DateTime.UtcNow - issue.CreatedAt).Days;
                emailBuilder.AppendFormat(@"
        <div class='issue open'>
            <h3><a href='{0}' class='issue-title'>#{1} {2}</a></h3>
            <div class='issue-stats'>
                <span class='issue-state state-open'>open</span> • 
                By <strong>{3}</strong> • 
                {4} comments • {5} reactions • 
                Created {6:MMM dd, yyyy} ({7} days ago)
            </div>",
                    issue.Url,
                    ExtractIssueNumber(issue.Url),
                    issue.Title,
                    issue.Author,
                    issue.CommentsCount,
                    issue.ReactionsCount,
                    issue.CreatedAt,
                    daysSinceCreated);

                // Labels
                if (issue.Labels.Any())
                {
                    emailBuilder.AppendLine(@"
            <div class='labels'>");
                    foreach (var label in issue.Labels)
                    {
                        emailBuilder.AppendFormat(@"<span class='label'>{0}</span>", label);
                    }
                    emailBuilder.AppendLine(@"
            </div>");
                }

                emailBuilder.AppendFormat(@"
            <div class='action-buttons'>
                <a href='{0}' class='feedback-button'>View on GitHub</a>
                <a href='https://www.feedbackflow.app/?source=auto&id={1}' class='feedback-button'>Open in FeedbackFlow</a>
            </div>
        </div>", issue.Url, reportId);
            }

            emailBuilder.AppendLine(@"
    </div>");
        }

        // Footer
        emailBuilder.AppendFormat(@"
    <div class='divider'></div>
    <p style='text-align: center; color: #7c7c7c; font-size: 0.9em;'>
        Generated by <a href='https://www.feedbackflow.app' style='color: #0366d6;'>FeedbackFlow</a> on {0:MMMM dd, yyyy}
    </p>
</body>
</html>", DateTime.UtcNow);

        return emailBuilder.ToString();
    }

    private static string ExtractIssueNumber(string issueUrl)
    {
        var segments = issueUrl.Split('/');
        return segments.LastOrDefault() ?? "0";
    }

    private static readonly HashSet<string> CommonStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him",
        "his", "how", "its", "may", "new", "now", "old", "see", "two", "who", "boy", "did", "have", "let", "put", "say", "she", "too", "use",
        "with", "this", "that", "from", "they", "know", "want", "been", "good", "much", "some", "time", "very", "when", "come", "here", "just",
        "like", "long", "make", "many", "over", "such", "take", "than", "them", "well", "were", "will", "your", "about", "after", "back", "other",
        "right", "think", "where", "being", "every", "great", "might", "shall", "still", "those", "under", "while", "issue", "issues", "bug", "bugs"
    };
}