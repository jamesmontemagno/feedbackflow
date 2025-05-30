using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.HackerNews;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public record HackerNewsAnalysis(string MarkdownResult, List<HackerNewsItem> Stories);

public class HackerNewsFeedbackService : FeedbackService, IHackerNewsFeedbackService
{
    private readonly string _storyId;

    public HackerNewsFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        string storyId,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _storyId = storyId;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        var processedId = UrlParsing.ExtractHackerNewsId(_storyId);

        if (string.IsNullOrWhiteSpace(processedId))
        {
            throw new InvalidOperationException("Please enter a valid Hacker News story ID or URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Hacker News comments...");

        var hnCode = Configuration["FeedbackApi:GetHackerNewsFeedbackCode"]
            ?? throw new InvalidOperationException("Hacker News API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the Hacker News API
        var getFeedbackUrl = $"{BaseUrl}/api/GetHackerNewsFeedback?code={Uri.EscapeDataString(hnCode)}&ids={Uri.EscapeDataString(processedId)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Parse the Hacker News response        
        var articleThread = JsonSerializer.Deserialize<List<List<HackerNewsItem>>>(responseContent)?.FirstOrDefault()
            ?? throw new InvalidOperationException("Failed to deserialize Hacker News response");

        if (!articleThread.Any())
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("No comments available", 0, null);
        }

        // Calculate total comments recursively (excluding the story post)
        int CountComments(IEnumerable<HackerNewsItem> items)
        {
            return items.Skip(1).Sum(item => 1 + (item.Kids?.Count ?? 0));
        }

        var totalComments = CountComments(articleThread);

        // Create HackerNewsAnalysis objects for each story thread
        var analyses = new List<HackerNewsAnalysis>
        {
            new HackerNewsAnalysis(
                MarkdownResult: string.Empty, // This will be filled in during AnalyzeComments
                Stories: articleThread
            )
        };

        return (responseContent, totalComments, analyses);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        var totalComments = commentCount ?? comments.Split("\n").Count(line => line.StartsWith("Comment by"));
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {totalComments} comments...");

        // Analyze the comments
        var markdownResult = await AnalyzeCommentsInternal("hackernews", comments, totalComments);

        // If we have analyses, update their MarkdownResult
        if (additionalData is List<HackerNewsAnalysis> analyses && analyses.Any())
        {
            analyses[0] = analyses[0] with { MarkdownResult = markdownResult };
            return (markdownResult, analyses);
        }

        return (markdownResult, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments) || comments == "No comments available")
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}