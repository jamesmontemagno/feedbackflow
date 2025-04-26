using FeedbackWebApp.Services.Feedback;

namespace FeedbackWebApp.Services.Interfaces;

public interface IManualFeedbackService : IFeedbackService
{
    string CustomPrompt { get; set; }
    string Content { get; set; }
}
