using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

internal static class FeedbackServiceExtensions
{
    /// <summary>
    /// Assigns a temporary, non-persisted prompt to a feedback service for a single request.
    /// </summary>
    public static IFeedbackService WithTemporaryPrompt(this IFeedbackService service, string? prompt)
    {
        if (service is FeedbackService concrete && !string.IsNullOrWhiteSpace(prompt))
        {
            concrete.SetTemporaryPrompt(prompt);
        }
        return service;
    }
}
