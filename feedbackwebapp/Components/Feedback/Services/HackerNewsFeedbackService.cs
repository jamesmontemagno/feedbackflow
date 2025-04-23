using SharedDump.Models.HackerNews;
using SharedDump.Utils;
using System.Text.Json;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public record HackerNewsAnalysis(string MarkdownResult, List<HackerNewsItem> Stories);

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

        // Parse the Hacker News response as List<List<HackerNewsItem>>
        var articleThreads = JsonSerializer.Deserialize<List<List<HackerNewsItem>>>(responseContent)
            ?? throw new InvalidOperationException("Failed to deserialize Hacker News response");

        if (!articleThreads.Any() || articleThreads.All(thread => !thread.Any()))
        {
            throw new InvalidOperationException("No comments found for the specified stories");
        }

        var analyses = new List<HackerNewsAnalysis>();
        var totalArticles = articleThreads.Count;
        
        for (var i = 0; i < articleThreads.Count; i++)
        {
            var thread = articleThreads[i];
            var story = thread.FirstOrDefault();
            if (story == null) continue;

            UpdateStatus(
                FeedbackProcessStatus.GatheringComments, 
                $"Analyzing article {i + 1} of {totalArticles}: {story.Title}"
            );

            var threadComments = string.Join("\n", thread.Skip(1).Select(comment => 
                $"Comment by {comment.By}: {comment.Text ?? ""}"
            ));

            var analysisText = $"Story: {story.Title}\n{threadComments}";
            var markdownResult = await AnalyzeComments("hackernews", analysisText);
            analyses.Add(new HackerNewsAnalysis(markdownResult, thread));
        }

        // Combine all analyses into a single markdown document with headers for each article
        var combinedMarkdown = string.Join("\n\n", analyses.Select((analysis, index) => 
            $"# Article {index + 1}: {analysis.Stories.FirstOrDefault()?.Title}\n\n{analysis.MarkdownResult}"
        ));

        return (combinedMarkdown, analyses);
    }
}