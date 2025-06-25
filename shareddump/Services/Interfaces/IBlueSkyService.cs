using SharedDump.Models.BlueSkyFeedback;

namespace SharedDump.Services.Interfaces;

/// <summary>
/// Interface for BlueSky feedback services
/// </summary>
public interface IBlueSkyService
{
    /// <summary>
    /// Gets BlueSky post and comments for a specific post
    /// </summary>
    /// <param name="postUrlOrId">Post URL or ID</param>
    /// <returns>BlueSky feedback response with posts and replies</returns>
    Task<BlueSkyFeedbackResponse?> GetBlueSkyPostAsync(string postUrlOrId);
    
    /// <summary>
    /// Sets credentials for authentication
    /// </summary>
    /// <param name="username">BlueSky username</param>
    /// <param name="appPassword">BlueSky app password</param>
    void SetCredentials(string username, string appPassword);
}
