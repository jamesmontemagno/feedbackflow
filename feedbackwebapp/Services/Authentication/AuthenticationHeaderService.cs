using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.Authentication;

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

            // Create a ClientPrincipal object matching what the Azure Function expects
            var clientPrincipal = new ClientPrincipal
            {
                IdentityProvider = GetIdentityProviderName(currentUser.AuthProvider),
                UserId = currentUser.ProviderUserId,
                UserDetails = currentUser.Email,
                Claims = new[]
                {
                    new ClientPrincipalClaim { Type = "name", Value = currentUser.Name },
                    new ClientPrincipalClaim { Type = "email", Value = currentUser.Email },
                    new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", Value = currentUser.Email },
                    new ClientPrincipalClaim { Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", Value = currentUser.Name }
                }.Where(c => !string.IsNullOrEmpty(c.Value)).ToArray()
            };

            // Serialize to JSON and encode as base64
            var json = JsonSerializer.Serialize(clientPrincipal, _jsonOptions);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            // Add the authentication header
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL", base64);
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
            "Password" => "password",
            _ => authProvider.ToLower()
        };
    }
}
