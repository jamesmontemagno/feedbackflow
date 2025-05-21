using System.Text.Json;
using SharedDump.Models.GitHub;
using FeedbackWebApp.Components.Feedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class GitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _url;

    public GitHubFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        string url,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _url = url;
    }

    public override async Task<(string rawComments, object? additionalData)> GetComments()
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            throw new InvalidOperationException("Please enter a valid GitHub URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub feedback...");

        var githubCode = Configuration["FeedbackApi:GetGitHubFeedbackCode"]
            ?? throw new InvalidOperationException("GitHub API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the GitHub API
        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubFeedback?code={Uri.EscapeDataString(githubCode)}&url={Uri.EscapeDataString(_url)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Determine if this is an issue/PR or a discussion
        if (_url.Contains("/discussions/"))
        {
            var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(responseContent);
            if (discussions == null || !discussions.Any())
            {
                UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
                return ("No comments available", null);
            }

            var allComments = string.Join("\n\n", discussions.Select(discussion =>
            {
                var comments = new List<string>
                {
                    $"Discussion: {discussion.Title}\nURL: {discussion.Url}"
                };

                if (discussion.Comments != null)
                {
                    comments.AddRange(discussion.Comments.Select(comment =>
                        $"Comment by {comment.Author}: {comment.Content}"
                    ));
                }

                return string.Join("\n", comments);
            }));

            return (allComments, discussions);
        }
        else
        {
            var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(responseContent);
            if (issues == null || !issues.Any())
            {
                UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
                return ("No comments available", null);
            }

            var allComments = string.Join("\n\n", issues.Select(issue =>
            {
                var comments = new List<string>
                {
                    $"Issue: {issue.Title}\nAuthor: {issue.Author}\nContent: {issue.Body}"
                };

                if (issue.Comments != null)
                {
                    comments.AddRange(issue.Comments.Select(comment =>
                        $"Comment by {comment.Author}: {comment.Content}"
                    ));
                }

                return string.Join("\n", comments);
            }));

            return (allComments, issues);
        }
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Calculate number of comments
        var totalComments = comments.Split("\n\n").Length;

        // Analyze the comments
        var markdownResult = await AnalyzeCommentsInternal("github", comments, totalComments);
        return (markdownResult, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments) || comments == "No comments available")
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, additionalData);
    }
}