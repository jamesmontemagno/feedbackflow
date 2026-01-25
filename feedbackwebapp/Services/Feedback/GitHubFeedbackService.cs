using System.Text.Json;
using SharedDump.Models.GitHub;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Feedback;

public class GitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly string _url;

    public GitHubFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        string url,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
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

        var githubCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("GitHub API code not configured");

        // Get comments from the GitHub API
        var getFeedbackUrl = $"{BaseUrl}/api/GetGitHubFeedback?code={Uri.EscapeDataString(githubCode)}&url={Uri.EscapeDataString(_url)}";
        var feedbackResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Get, getFeedbackUrl);
        
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        var totalComments = 0;

        // The API now returns either an array of GithubIssueModel or GithubDiscussionModel
        // Try to deserialize as issues first (covers both issues and PRs)
        try
        {
            var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(responseContent);
            if (issues != null && issues.Any())
            {
                totalComments = issues.Sum(issue => 
                    (issue.Comments?.Length ?? 0) + 1); // +1 for the issue body

                return (responseContent, totalComments, issues);
            }
        }
        catch (JsonException)
        {
            // If it fails, try deserializing as discussions
            try
            {
                var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(responseContent);
                if (discussions != null && discussions.Any())
                {
                    totalComments = discussions.Sum(discussion => 
                        (discussion.Comments?.Length ?? 0) + 1); // +1 for the discussion body

                    return (responseContent, totalComments, discussions);
                }
            }
            catch (JsonException)
            {
                // If both fail, return empty
                UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
                return ("No comments available", 0, null);
            }
        }

        // If we get here, no valid data was found
        UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
        return ("No comments available", 0, null);
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
