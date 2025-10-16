namespace FeedbackFlow.MCP.Shared;

/// <summary>
/// Interface for providing authentication tokens for FeedbackFlow API calls
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Gets the authentication token (API key or Bearer token)
    /// </summary>
    /// <returns>The authentication token, or null if not available</returns>
    string? GetAuthenticationToken();

    /// <summary>
    /// Gets a user-friendly error message when authentication is not available
    /// </summary>
    /// <returns>Error message to display to the user</returns>
    string GetAuthenticationErrorMessage();
}