// filepath: c:\GitHub\feedbackflow\feedbackwebapp\Services\Mock\MockManualFeedbackService.cs
using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Services.Mock;

namespace FeedbackWebApp.Services.Mock;

public class MockManualFeedbackService : FeedbackService, IManualFeedbackService
{
    public string CustomPrompt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public MockManualFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authenticationHeaderService,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate) { }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null)
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Processing manual input...");
        await Task.Delay(500); // Simulate network delay

        if (string.IsNullOrWhiteSpace(Content))
        {
            return ("", 0, null);
        }

        return (Content, 1, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No content to analyze");
            return ("## No Content Available\n\nThere is no content to analyze at this time.", null);
        }

        // Simulate analysis time
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing manual input...");
        await Task.Delay(1000);

        // Use shared mock analysis provider
        var mockMarkdown = MockAnalysisProvider.GetMockAnalysis("manual", commentCount ?? 1);

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return (mockMarkdown, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere is no content to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
