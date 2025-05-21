// filepath: c:\GitHub\feedbackflow\feedbackwebapp\Services\Feedback\ManualFeedbackService.cs
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class ManualFeedbackService : FeedbackService, IManualFeedbackService
{
    public string CustomPrompt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ManualFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        string content,
        string? customPrompt = null,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        Content = content;
        if (!string.IsNullOrEmpty(customPrompt))
        {
            CustomPrompt = customPrompt;
        }
    }

    public override async Task<(string rawComments, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Processing manual input...");
        
        await Task.Delay(100); // Small delay to maintain async context

        if (string.IsNullOrWhiteSpace(Content))
        {
            return ("", null);
        }

        // For manual content, we just return the raw content
        return (Content, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No content to analyze");
            return ("## No Content Available\n\nThere is no content to analyze at this time.", null);
        }

        // For manual service, we use "manual" as the service type and pass custom prompt
        var result = await AnalyzeCommentsInternal("manual", comments, 1, CustomPrompt);
        return (result, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No content to analyze");
            return ("## No Content Available\n\nThere is no content to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, additionalData);
    }
}
