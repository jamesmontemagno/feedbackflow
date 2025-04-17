namespace FeedbackWebApp.Components.Feedback.Services;

public interface IFeedbackService
{
    Task<(string markdownResult, object? additionalData)> GetFeedback();
}

public interface IYouTubeFeedbackService : IFeedbackService { }
public interface IHackerNewsFeedbackService : IFeedbackService { }
public interface IGitHubFeedbackService : IFeedbackService { }