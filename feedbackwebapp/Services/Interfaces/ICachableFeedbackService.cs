using FeedbackWebApp.Services.Feedback;

namespace FeedbackWebApp.Services.Interfaces;

/// <summary>
/// Interface for feedback services that can expose their last fetched comments for caching/reanalysis
/// </summary>
public interface ICachableFeedbackService
{
    /// <summary>
    /// Gets the last fetched comments snapshot from the most recent GetComments() call
    /// Returns null if no comments have been fetched yet
    /// </summary>
    (string comments, int commentCount, object? additionalData)? LastCommentsSnapshot { get; }
}