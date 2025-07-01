using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Authentication;
using SharedDump.Services.Authentication;

namespace FeedbackFunctions.Services.Authentication;

/// <summary>
/// Authentication middleware for processing Azure Easy Auth headers
/// </summary>
public class AuthenticationMiddleware
{
    private readonly IAuthUserTableService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(
        IAuthUserTableService userService, 
        IConfiguration configuration, 
        ILogger<AuthenticationMiddleware> logger)
    {
        _userService = userService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get or create an authenticated user from the request headers
    /// </summary>
    /// <param name="req">HTTP request with authentication headers</param>
    /// <returns>Authenticated user or null if not authenticated</returns>
    public async Task<AuthenticatedUser?> GetOrCreateUserAsync(HttpRequestData req)
    {
        try
        {
            // Check if authentication is bypassed for development
            var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
            var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";
            
            if (bypassAuth && isDevelopment)
            {
                _logger.LogInformation("Authentication bypassed for development environment");
                return CreateDevelopmentUser();
            }

            // Read the principal header from Azure Easy Auth
            if (!req.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var principalHeaders))
            {
                _logger.LogWarning("No X-MS-CLIENT-PRINCIPAL header found in request");
                return null;
            }

            var principalHeader = principalHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(principalHeader))
            {
                _logger.LogWarning("X-MS-CLIENT-PRINCIPAL header is empty");
                return null;
            }

            // Decode the base64-encoded principal
            var decoded = Convert.FromBase64String(principalHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var clientPrincipal = JsonSerializer.Deserialize<ClientPrincipal>(json);

            if (clientPrincipal == null)
            {
                _logger.LogWarning("Failed to deserialize client principal");
                return null;
            }

            // Extract provider and user information
            var provider = GetProviderFromIdentityProvider(clientPrincipal.IdentityProvider);
            var providerUserId = clientPrincipal.UserId;
            var email = clientPrincipal.UserDetails;
            var name = clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "given_name")?.Value ?? 
                      email;

            if (string.IsNullOrEmpty(providerUserId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Missing required user information from client principal");
                return null;
            }

            // Get or create user
            var user = await _userService.GetUserByProviderAsync(provider, providerUserId);
            if (user == null)
            {
                _logger.LogInformation("Creating new user for {Provider} provider with ID {ProviderUserId}", provider, providerUserId);
                user = new AuthUserEntity(provider, providerUserId, email, name);
                await _userService.CreateOrUpdateUserAsync(user);
                await _userService.UpdateEmailIndexAsync(email, user.UserId, provider, providerUserId);
            }
            else
            {
                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                await _userService.CreateOrUpdateUserAsync(user);
            }

            return new AuthenticatedUser(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing authentication");
            return null;
        }
    }

    /// <summary>
    /// Create a development user for auth bypass scenarios
    /// </summary>
    /// <returns>Development authenticated user</returns>
    private static AuthenticatedUser CreateDevelopmentUser()
    {
        return new AuthenticatedUser
        {
            UserId = "dev-user-id",
            Email = "dev@example.com",
            Name = "Development User",
            AuthProvider = "Development",
            ProviderUserId = "dev-provider-id",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Map identity provider names to standard provider names
    /// </summary>
    /// <param name="identityProvider">Identity provider from Azure Easy Auth</param>
    /// <returns>Standardized provider name</returns>
    private static string GetProviderFromIdentityProvider(string identityProvider)
    {
        return identityProvider switch
        {
            "microsoftaccount" => "Microsoft",
            "google" => "Google",
            "github" => "GitHub",
            "facebook" => "Facebook",
            "twitter" => "Twitter",
            _ => identityProvider
        };
    }
}