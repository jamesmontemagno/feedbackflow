using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Authentication;
using FeedbackFunctions.Services.Authentication;

namespace FeedbackFunctions.Middleware;

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
    /// Get an authenticated user from the request headers (read-only operation)
    /// </summary>
    /// <param name="req">HTTP request with authentication headers</param>
    /// <returns>Authenticated user or null if not authenticated</returns>
    public async Task<AuthenticatedUser?> GetUserAsync(HttpRequestData req)
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
                // Debug: Log all headers to see what we're getting
                foreach (var header in req.Headers)
                {
                    _logger.LogInformation("[DEBUG] Header: {HeaderName} = {HeaderValue}", header.Key, string.Join(", ", header.Value));
                }
                return null;
            }

            var principalHeader = principalHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(principalHeader))
            {
                _logger.LogWarning("X-MS-CLIENT-PRINCIPAL header is empty");
                return null;
            }

            _logger.LogInformation("[DEBUG] Received X-MS-CLIENT-PRINCIPAL header: {Header}", principalHeader);

            // Decode the base64-encoded principal
            var decoded = Convert.FromBase64String(principalHeader);
            var json = Encoding.UTF8.GetString(decoded);
            _logger.LogInformation("[DEBUG] Decoded JSON: {Json}", json);
            var clientPrincipal = JsonSerializer.Deserialize<ClientPrincipal>(json);

            if (clientPrincipal == null)
            {
                _logger.LogWarning("Failed to deserialize client principal");
                return null;
            }

            // Extract provider and user information
            var provider = GetProviderFromIdentityProvider(clientPrincipal.GetEffectiveIdentityProvider());
            var providerUserId = clientPrincipal.GetEffectiveUserId();
            var email = clientPrincipal.GetEffectiveUserDetails();
            var name = clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "given_name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "urn:github:login")?.Value ??  // Fallback to GitHub username
                      email ?? "Unknown User";
            
            // For GitHub, if no email is available, log it but continue without email
            if (string.IsNullOrEmpty(email) && provider == "GitHub")
            {
                var githubLogin = clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "urn:github:login")?.Value;
                _logger.LogInformation("[DEBUG] GitHub user '{GitHubLogin}' has no email address available", githubLogin);
                email = null; // Explicitly set to null for GitHub users without email
            }
            
            _logger.LogInformation("[DEBUG] Extracted values - Provider: {Provider}, UserId: {UserId}, Email: {Email}, Name: {Name}", 
                provider, providerUserId, email ?? "(none)", name);
            
            // Extract profile image URL based on provider
            var profileImageUrl = GetProfileImageUrl(clientPrincipal.GetEffectiveIdentityProvider(), clientPrincipal.Claims);

            // Only require providerUserId - email is now optional
            if (string.IsNullOrEmpty(providerUserId))
            {
                _logger.LogWarning("Missing required user information from client principal. UserId: '{UserId}'", providerUserId);
                return null;
            }

            // Get existing user (read-only operation)
            var user = await _userService.GetUserByProviderAsync(provider, providerUserId);
            if (user == null)
            {
                _logger.LogWarning("User not found for {Provider} provider with ID {ProviderUserId}. User must be registered first.", provider, providerUserId);
                return null;
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
    /// Create a new user from the request headers
    /// </summary>
    /// <param name="req">HTTP request with authentication headers</param>
    /// <returns>Newly created authenticated user or null if creation failed</returns>
    public async Task<AuthenticatedUser?> CreateUserAsync(HttpRequestData req)
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
            var provider = GetProviderFromIdentityProvider(clientPrincipal.GetEffectiveIdentityProvider());
            var providerUserId = clientPrincipal.GetEffectiveUserId();
            var email = clientPrincipal.GetEffectiveUserDetails();
            var name = clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "given_name")?.Value ?? 
                      clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "urn:github:login")?.Value ??  // Fallback to GitHub username
                      email ?? "Unknown User";
            
            // For GitHub, if no email is available, log it but continue without email
            if (string.IsNullOrEmpty(email) && provider == "GitHub")
            {
                var githubLogin = clientPrincipal.Claims?.FirstOrDefault(c => c.Type == "urn:github:login")?.Value;
                _logger.LogInformation("[DEBUG] GitHub user '{GitHubLogin}' has no email address available", githubLogin);
                email = null; // Explicitly set to null for GitHub users without email
            }
            
            // Extract profile image URL based on provider
            var profileImageUrl = GetProfileImageUrl(clientPrincipal.GetEffectiveIdentityProvider(), clientPrincipal.Claims);

            // Only require providerUserId - email is now optional
            if (string.IsNullOrEmpty(providerUserId))
            {
                _logger.LogWarning("Missing required user information from client principal. UserId: '{UserId}'", providerUserId);
                return null;
            }

            // Check if user already exists
            var existingUser = await _userService.GetUserByProviderAsync(provider, providerUserId);
            if (existingUser != null)
            {
                existingUser.LastLoginAt = DateTime.UtcNow;
                existingUser.ProfileImageUrl = profileImageUrl; // Update profile image URL if available
                await _userService.CreateOrUpdateUserAsync(existingUser);

                _logger.LogWarning("User already exists for {Provider} provider with ID {ProviderUserId}. Use GetUserAsync instead.", provider, providerUserId);
                return new AuthenticatedUser(existingUser);
            }

            // Create new user
            _logger.LogInformation("Creating new user for {Provider} provider with ID {ProviderUserId}", provider, providerUserId);
            var user = new AuthUserEntity(provider, providerUserId, email, name)
            {
                ProfileImageUrl = profileImageUrl
            };
            await _userService.CreateOrUpdateUserAsync(user);

            return new AuthenticatedUser(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return null;
        }
    }

    /// <summary>
    /// Get or create an authenticated user from the request headers (deprecated - use GetUserAsync or CreateUserAsync)
    /// </summary>
    /// <param name="req">HTTP request with authentication headers</param>
    /// <returns>Authenticated user or null if not authenticated</returns>
    [Obsolete("Use GetUserAsync for authentication or CreateUserAsync for user registration instead")]
    public async Task<AuthenticatedUser?> GetOrCreateUserAsync(HttpRequestData req)
    {
        // For backward compatibility, first try to get existing user
        var existingUser = await GetUserAsync(req);
        if (existingUser != null)
        {
            return existingUser;
        }

        // If user doesn't exist, create new user
        return await CreateUserAsync(req);
    }

    /// <summary>
    /// Create a development user for auth bypass scenarios
    /// </summary>
    /// <returns>Development authenticated user</returns>
    private AuthenticatedUser CreateDevelopmentUser()
    {
        var devUserId = _configuration.GetValue<string>("Development:UserId") ?? "dev-user-id";
        var devEmail = _configuration.GetValue<string>("Development:Email") ?? "dev@example.com";
        var devName = _configuration.GetValue<string>("Development:Name") ?? "Development User";
        
        return new AuthenticatedUser
        {
            UserId = devUserId,
            Email = devEmail,
            Name = devName,
            AuthProvider = "Development",
            ProviderUserId = devUserId,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Extract profile image URL from provider claims
    /// </summary>
    /// <param name="identityProvider">Identity provider from Azure Easy Auth</param>
    /// <param name="claims">User claims</param>
    /// <returns>Profile image URL or null</returns>
    private static string? GetProfileImageUrl(string identityProvider, ClientPrincipalClaim[]? claims)
    {
        if (claims == null) return null;

        return identityProvider switch
        {
            "aad" => claims.FirstOrDefault(c => c.Type == "picture")?.Value,
            "google" => claims.FirstOrDefault(c => c.Type == "picture")?.Value,
            "github" => claims.FirstOrDefault(c => c.Type == "urn:github:avatar_url")?.Value,
            "facebook" => claims.FirstOrDefault(c => c.Type == "urn:facebook:picture")?.Value,
            "twitter" => claims.FirstOrDefault(c => c.Type == "urn:twitter:profile_image_url_https")?.Value,
            _ => null
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
            "aad" => "Microsoft",
            "google" => "Google",
            "github" => "GitHub",
            "facebook" => "Facebook",
            "twitter" => "Twitter",
            "password" => "Password",
            _ => identityProvider
        };
    }
}
