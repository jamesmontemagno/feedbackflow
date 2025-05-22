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

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
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
        var totalComments = 0;

        // Determine if this is an issue/PR or a discussion
        if (_url.Contains("/discussions/"))
        {
            var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(responseContent);
            if (discussions == null || !discussions.Any())
            {
                UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
                return ("No comments available", 0, null);
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
                    totalComments += discussion.Comments.Count();
                }

                return string.Join("\n", comments);
            }));

            return (allComments, totalComments, discussions);
        }
        else
        {
            var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(responseContent);
            if (issues == null || !issues.Any())
            {
                UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
                return ("No comments available", 0, null);
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
                    totalComments += issue.Comments.Count() + 1; // +1 for the issue body
                }

                return string.Join("\n", comments);
            }));

            return (allComments, totalComments, issues);
        }
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {commentCount ?? 0} comments...");

        // Analyze comments using the shared AnalyzeComments method
        var markdown = await AnalyzeCommentsInternal("github", comments, commentCount ?? 0);
        return (markdown, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}