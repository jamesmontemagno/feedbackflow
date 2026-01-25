using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Services.Mock;

namespace FeedbackWebApp.Services.Mock;

public class MockYouTubeFeedbackService(
    IHttpClientFactory http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    IAuthenticationHeaderService authenticationHeaderService,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate), IYouTubeFeedbackService
{
    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock YouTube comments...");
        await Task.Delay(1000); // Simulate network delay

        // Use shared mock data provider
        var mockVideos = MockDataProvider.YouTube.GetMockVideos();
        var allComments = MockDataProvider.YouTube.GetFormattedComments();
        var totalComments = mockVideos.Sum(v => v.Comments?.Count ?? 0);

        return (allComments, totalComments, mockVideos);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        // Simulate analysis time
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock YouTube comments...");
        await Task.Delay(1000); 
        
        // Use shared mock analysis provider
        var mockAnalysis = MockAnalysisProvider.GetMockAnalysis("youtube", commentCount ?? 0);

        UpdateStatus(FeedbackProcessStatus.Completed, "YouTube analysis completed");
        return (mockAnalysis, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        // Analyze comments
        var result = await AnalyzeComments(comments, commentCount, additionalData);
        
        // Ensure completion status is set
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed successfully");
        
        return result;
    }
}

public class MockHackerNewsFeedbackService(
    IHttpClientFactory http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    IAuthenticationHeaderService authenticationHeaderService,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate), IHackerNewsFeedbackService
{
    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Hacker News comments...");
        await Task.Delay(1000); // Simulate network delay

        // Use shared mock data provider for consistent comments
        var mockComments = MockDataProvider.HackerNews.GetFormattedComments();
        var mockArticleThreads = MockDataProvider.HackerNews.GetMockItemThreads();

        // Count total comments by counting lines that start with "Comment by"
        int commentCount = mockComments.Split('\n').Count(line => line.StartsWith("Comment by"));

        return (mockComments, commentCount, mockArticleThreads);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock Hacker News comments...");
        await Task.Delay(1000); // Simulate analysis time
        
        // Use the provided comment count or calculate it
        int totalComments = commentCount ?? comments.Split('\n').Count(line => line.StartsWith("Comment by"));
        
        // Use shared mock analysis provider
        var mockAnalysis = MockAnalysisProvider.GetMockAnalysis("hackernews", totalComments);

        UpdateStatus(FeedbackProcessStatus.Completed, "Hacker News analysis completed");
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

        // Analyze comments
        var result = await AnalyzeComments(comments, commentCount, additionalData);
        
        // Ensure completion status is set
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed successfully");
        
        return result;
    }
}
