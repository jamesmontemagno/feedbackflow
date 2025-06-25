using SharedDump.Models.TwitterFeedback;

namespace SharedDump.Services.Interfaces;

/// <summary>
/// Interface for Twitter/X feedback services
/// </summary>
public interface ITwitterService
{
    /// <summary>
    /// Gets Twitter thread and comments for a specific tweet
    /// </summary>
    /// <param name="tweetUrlOrId">Tweet URL or ID</param>
    /// <returns>Twitter feedback response with tweets and replies</returns>
    Task<TwitterFeedbackResponse?> GetTwitterThreadAsync(string tweetUrlOrId);
}
