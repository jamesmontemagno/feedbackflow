using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.GitHub;
using SharedDump.Services.Mock;

namespace FeedbackWebApp.Services.Mock;

public class MockGitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    private readonly MockGitHubService _mockGitHubService = new();

    public MockGitHubFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authenticationHeaderService,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub feedback...");
        
        // Simulate network delay
        await Task.Delay(1500);

        // Use the shared mock service to get data
        var (repoOwner, repoName) = ("sample", "repo");

        var issuesTask = _mockGitHubService.GetIssuesAsync(repoOwner, repoName, Array.Empty<string>());
        var discussionsTask = _mockGitHubService.GetDiscussionsAsync(repoOwner, repoName);
        var pullsTask = _mockGitHubService.GetPullRequestsAsync(repoOwner, repoName, Array.Empty<string>());

        await Task.WhenAll(issuesTask, discussionsTask, pullsTask);

        var issues = await issuesTask;
        var discussions = await discussionsTask;
        var pulls = await pullsTask;

        // Build response data object
        var responseData = new { Issues = issues, Discussions = discussions, PullRequests = pulls };

        // Build the comments string
        var allComments = string.Join("\n\n", issues.Select(issue =>
        {
            var comments = new List<string>
            {
                $"Issue: {issue.Title}",
                $"Description: {issue.Body}"
            };

            if (issue.Comments != null)
            {
                comments.AddRange(issue.Comments.Select(comment =>
                    $"Comment by {comment.Author}: {comment.Content}"
                ));
            }

            return string.Join("\n", comments);
        }));

        allComments += "\n\n" + string.Join("\n\n", discussions.Select(discussion =>
        {
            var comments = new List<string>
            {
                $"Discussion: {discussion.Title}"
            };

            if (discussion.Comments != null)
            {
                comments.AddRange(discussion.Comments.Select(comment =>
                    $"Comment by {comment.Author}: {comment.Content}"
                ));
            }

            return string.Join("\n", comments);
        }));

        allComments += "\n\n" + string.Join("\n\n", pulls.Select(pull =>
        {
            var comments = new List<string>
            {
                $"Pull Request: {pull.Title}",
                $"Description: {pull.Body}"
            };

            if (pull.Comments != null)
            {
                comments.AddRange(pull.Comments.Select(comment =>
                    $"Comment by {comment.Author}: {comment.Content}"
                ));
            }

            return string.Join("\n", comments);
        }));

        // Count total comments (issue comments + discussion comments + PR comments + descriptions)
        int totalComments = 
            issues.Sum(i => (i.Comments?.Length ?? 0) + 1) + // Issue comments + descriptions
            discussions.Sum(d => d.Comments?.Length ?? 0) + // Discussion comments
            pulls.Sum(p => (p.Comments?.Length ?? 0) + 1); // PR comments + descriptions

        return (allComments, totalComments, responseData);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing GitHub feedback...");
        await Task.Delay(2000);

        // Use provided comment count or calculate
        int totalComments = commentCount ?? comments.Split('\n').Count(line => line.StartsWith("Comment by"));

        // Use shared mock analysis provider
        var mockAnalysis = MockAnalysisProvider.GetMockAnalysis("github", totalComments);

        UpdateStatus(FeedbackProcessStatus.Completed, "GitHub analysis completed");
        return (mockAnalysis, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments found");
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments with count
        var result = await AnalyzeComments(comments, commentCount, additionalData);
        
        // Ensure completion status is set
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed successfully");
        
        return result;
    }
}
