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
    }    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Processing manual input...");
        
        if (string.IsNullOrWhiteSpace(Content))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No content to analyze");
            return ("## No Content Available\n\nThere is no content to analyze at this time.", null);
        }
        
        // For manual service, we always use "manual" as the service type
        // and pass the custom prompt separately
        var result = await AnalyzeComments("manual", Content, 1, CustomPrompt);
        
        return (result, null);
    }
}
