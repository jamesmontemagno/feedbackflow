using SharedDump.Models.HackerNews;
using SharedDump.Utils;
using System.Text.Json;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public class HackerNewsFeedbackService : FeedbackService, IHackerNewsFeedbackService
{
    private readonly string _storyIds;

    public HackerNewsFeedbackService(
        HttpClient http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        string storyIds,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _storyIds = storyIds;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
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

        // Parse the Hacker News response
        var stories = JsonSerializer.Deserialize<List<HackerNewsItem>>(responseContent);
        
        if (stories == null || !stories.Any())
        {
            throw new InvalidOperationException("No comments found for the specified stories");
        }

        var totalComments = stories.Sum(s => s.Kids?.Count ?? 0);
        UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Found {totalComments} comments across {stories.Count} Hacker News stories...");

        // Build our analysis request with all comments
        var allComments = string.Join("\n\n", stories.Select(story =>
        {
            var kids = story.Kids ?? new List<int>();
            return $"Story: {story.Title}\n" + string.Join("\n", kids.Select(c => $"Comment by {story.By}: {story.Text ?? ""}"));
        }));

        // Analyze the comments
        var markdownResult = await AnalyzeComments("hackernews", allComments);
        return (markdownResult, stories);
    }
}