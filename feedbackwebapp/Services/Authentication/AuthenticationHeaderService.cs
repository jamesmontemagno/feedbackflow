using System.Text.Json;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Service for adding authentication headers to HTTP requests
/// </summary>
public interface IAuthenticationHeaderService
{
    /// <summary>
    /// Add authentication headers to the request for Azure Functions
    /// </summary>
    /// <param name="request">HTTP request message</param>
    Task AddAuthenticationHeadersAsync(HttpRequestMessage request);
}

/// <summary>
/// Implementation of authentication header service using Azure Easy Auth format
/// </summary>
public class AuthenticationHeaderService : IAuthenticationHeaderService
{
    private readonly IAuthenticationService _authService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthenticationHeaderService(IAuthenticationService authService)
    {
        _authService = authService;
    }

    /// <inheritdoc />
    public async Task AddAuthenticationHeadersAsync(HttpRequestMessage request)
    {
        try
        {
            // Check if user is authenticated
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                return; // No authentication headers needed for unauthenticated requests
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return;
            }

            // Create a client principal object similar to what Azure Easy Auth would create
            var clientPrincipal = new
            {
                userId = currentUser.ProviderUserId,
                userDetails = currentUser.Email,
                identityProvider = GetIdentityProviderName(currentUser.AuthProvider),
                claims = new[]
                {
                    new { type = "name", value = currentUser.Name },
                    new { type = "email", value = currentUser.Email },
                    new { type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", value = currentUser.Email },
                    new { type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", value = currentUser.Name }
                }.Where(c => !string.IsNullOrEmpty(c.value)).ToArray()
            };

            // Serialize and encode the principal
            var principalJson = JsonSerializer.Serialize(clientPrincipal, _jsonOptions);
            var principalBytes = System.Text.Encoding.UTF8.GetBytes(principalJson);
            var principalBase64 = Convert.ToBase64String(principalBytes);

            // Add the authentication header
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL", principalBase64);
        }
        catch (Exception ex)
        {
            // Log error but continue without authentication headers rather than failing the request
            Console.WriteLine($"Error adding authentication headers: {ex.Message}");
        }
    }

    /// <summary>
    /// Map standard provider names to identity provider names
    /// </summary>
    /// <param name="authProvider">Standard provider name</param>
    /// <returns>Identity provider name for Azure Easy Auth</returns>
    private static string GetIdentityProviderName(string authProvider)
    {
        return authProvider switch
        {
            "Microsoft" => "aad",
            "Google" => "google",
            "GitHub" => "github",
            "Facebook" => "facebook",
            "Twitter" => "twitter",
            _ => authProvider.ToLower()
        };
    }
}
