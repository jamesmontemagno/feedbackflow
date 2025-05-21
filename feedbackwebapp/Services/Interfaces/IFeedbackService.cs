namespace FeedbackWebApp.Services.Interfaces;

public interface IFeedbackService
{
    /// <summary>
    /// Gets raw comments directly from the source
    /// </summary>
    /// <returns>The raw comments and any additional data</returns>
    Task<(string rawComments, object? additionalData)> GetComments();

    /// <summary>
    /// Analyzes comments to produce insights
    /// </summary>
    /// <param name="comments">The comments to analyze</param>
    /// <param name="additionalData">Optional additional data to help with analysis</param>
    /// <returns>Analysis result in markdown format and any additional processed data</returns>
    Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null);

    /// <summary>
    /// Gets and analyzes feedback in a single operation
    /// </summary>
    /// <returns>Analysis result in markdown format and any additional data</returns>
    Task<(string markdownResult, object? additionalData)> GetFeedback();
}

public interface IYouTubeFeedbackService : IFeedbackService { }
public interface IHackerNewsFeedbackService : IFeedbackService { }
public interface IGitHubFeedbackService : IFeedbackService { }
public interface IRedditFeedbackService : IFeedbackService { }
public interface ITwitterFeedbackService : IFeedbackService { }