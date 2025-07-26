using SharedDump.Models.Authentication;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Interface for authentication services
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Check if the user is currently authenticated
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Get the current authenticated user details
    /// </summary>
    /// <returns>Authenticated user or null if not authenticated</returns>
    Task<AuthenticatedUser?> GetCurrentUserAsync();

    /// <summary>
    /// Get the access token for API calls (primarily for OAuth providers)
    /// </summary>
    /// <returns>Access token or null if not available</returns>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Get the login URL for the specified provider
    /// </summary>
    /// <param name="provider">Authentication provider (e.g., "Microsoft", "Google")</param>
    /// <param name="redirectUrl">URL to redirect to after authentication</param>
    /// <returns>Login URL</returns>
    string GetLoginUrl(string provider, string? redirectUrl = null);

    /// <summary>
    /// Log out the current user
    /// </summary>
    /// <returns>Task representing the logout operation</returns>
    Task LogoutAsync();

    /// <summary>
    /// Handle post-login user registration for OAuth providers
    /// This should be called after a user completes an OAuth login flow
    /// </summary>
    /// <returns>True if registration was successful, false otherwise</returns>
    Task<bool> HandlePostLoginRegistrationAsync();

    /// <summary>
    /// Event triggered when authentication state changes
    /// </summary>
    event EventHandler<bool>? AuthenticationStateChanged;
}