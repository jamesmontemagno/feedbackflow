using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.HackerNews;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public record HackerNewsAnalysis(string MarkdownResult, List<HackerNewsItem> Stories);

public class HackerNewsFeedbackService : FeedbackService, IHackerNewsFeedbackService
{
    private readonly string _storyIds;

    public HackerNewsFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        string storyIds,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _storyIds = storyIds;
    }

    public override async Task<(string rawComments, object? additionalData)> GetComments()
    {
        var processedIds = UrlParsing.ExtractHackerNewsId(_storyIds);

        if (string.IsNullOrWhiteSpace(processedIds))
        {
            throw new InvalidOperationException("Please enter at least one valid Hacker News story ID or URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Hacker News comments...");

        var hnCode = Configuration["FeedbackApi:GetHackerNewsFeedbackCode"]
            ?? throw new InvalidOperationException("Hacker News API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the Hacker News API
        var getFeedbackUrl = $"{BaseUrl}/api/GetHackerNewsFeedback?code={Uri.EscapeDataString(hnCode)}&ids={Uri.EscapeDataString(processedIds)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Parse the Hacker News response as List<List<HackerNewsItem>>        
        var articleThreads = JsonSerializer.Deserialize<List<List<HackerNewsItem>>>(responseContent)
            ?? throw new InvalidOperationException("Failed to deserialize Hacker News response");

        if (!articleThreads.Any() || articleThreads.All(thread => !thread.Any()))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("No comments available", null);
        }

        var allComments = string.Join("\n\n", articleThreads.Select(thread =>
        {
            var story = thread.FirstOrDefault();
            if (story == null) return string.Empty;

            var threadComments = string.Join("\n", thread.Skip(1).Select(comment => 
                $"Comment by {comment.By}: {comment.Text ?? ""}"
            ));

            return $"Story: {story.Title}\n{threadComments}";
        }));

        return (allComments, articleThreads);
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
        var markdownResult = await AnalyzeCommentsInternal("hackernews", comments, totalComments);
        return (markdownResult, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments) || comments == "No comments available")
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, additionalData);
    }
}