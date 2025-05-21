using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Mock;

public class MockAutoDataSourceFeedbackService : FeedbackService, IAutoDataSourceFeedbackService
{
    public MockAutoDataSourceFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Simulating auto source analysis...");
        await Task.Delay(2000); // Simulate some work

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Processing feedback...");
        await Task.Delay(2000); // Simulate more work

        return ("This is a mock auto source analysis result.\n\n- Point 1\n- Point 2\n", null);
    }
}
