using SharedDump.Models.Reddit;
using System.Text.Json;
using SharedDump.Utils;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class RedditFeedbackService : FeedbackService, IRedditFeedbackService
{
    private readonly string[] _threadIds;

    public RedditFeedbackService(
        string[] threadIds,
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _threadIds = threadIds;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        var processedIds = UrlParsing.ExtractRedditId(_threadIds);

        if (string.IsNullOrWhiteSpace(processedIds))
        {
            throw new InvalidOperationException("Please enter at least one valid Reddit thread ID or URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Reddit comments...");

        var redditCode = Configuration["FeedbackApi:GetRedditFeedbackCode"]
            ?? throw new InvalidOperationException("Reddit API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the Reddit API
        var getFeedbackUrl = $"{BaseUrl}/api/GetRedditFeedback?code={Uri.EscapeDataString(redditCode)}&threads={Uri.EscapeDataString(processedIds)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Parse the Reddit response
        var threads = JsonSerializer.Deserialize<List<RedditThreadModel>>(responseContent);

        if (threads == null || !threads.Any())
        {
            throw new InvalidOperationException("No comments found in the specified threads");
        }

        int CountCommentsAndReplies(List<RedditCommentModel>? comments)
        {
            if (comments == null || !comments.Any()) return 0;
            return comments.Count + comments.Sum(c => CountCommentsAndReplies(c.Replies));
        }

        var totalComments = threads.Sum(t => CountCommentsAndReplies(t.Comments));
        UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Found {totalComments} comments and replies across {threads.Count} Reddit threads...");

        // Build our analysis request with all threads and comments
        var allComments = string.Join("\n\n", threads.Select(thread =>
        {
            var comments = thread.Comments.Select(c => $"Comment by {c.Author}: {c.Body}");
            return $"Thread: {thread.Title}\n{string.Join("\n", comments)}";
        }));

        // Analyze the comments        
        var markdownResult = await AnalyzeComments("reddit", allComments, totalComments);
        return (markdownResult, threads);
    }
}