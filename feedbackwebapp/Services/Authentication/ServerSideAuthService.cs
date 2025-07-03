using Microsoft.Extensions.Configuration;
using SharedDump.Models.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Server-side Azure Easy Auth implementation that uses HttpContext and cookies
/// </summary>
public class ServerSideAuthService : IAuthenticationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerSideAuthService> _logger;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public ServerSideAuthService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<ServerSideAuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync()
    {
        // Check if auth is bypassed for development
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

        if (bypassAuth && isDevelopment)
        {
            return true;
        }

        try
        {
            var userInfo = await GetEasyAuthUserAsync();
            return userInfo != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking authentication status");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        // Check if auth is bypassed for development
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

        if (bypassAuth && isDevelopment)
        {
            return CreateDevelopmentUser();
        }

        try
        {
            return await GetEasyAuthUserAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting current user");
            return null;
        }
    }

    /// <inheritdoc />
    public string GetLoginUrl(string provider, string? redirectUrl = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext not available");

        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var providerPath = provider.ToLower() switch
        {
            "microsoft" => "aad",
            "google" => "google",
            "github" => "github",
            "facebook" => "facebook",
            "twitter" => "twitter",
            _ => provider.ToLower()
        };

        var loginUrl = $"{baseUrl}/.auth/login/{providerPath}";
        
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            var encodedRedirect = Uri.EscapeDataString(redirectUrl);
            loginUrl += $"?post_login_redirect_url={encodedRedirect}";
        }
        else
        {
            // Default redirect to home page
            var defaultRedirect = Uri.EscapeDataString($"{baseUrl}/");
            loginUrl += $"?post_login_redirect_url={defaultRedirect}";
        }

        return loginUrl;
    }

    /// <inheritdoc />
    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext not available");

        // Trigger authentication state change
        AuthenticationStateChanged?.Invoke(this, false);
        
        // Redirect to logout endpoint - Azure Easy Auth will handle cookie cleanup
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var logoutUrl = $"{baseUrl}/.auth/logout";
        
        httpContext.Response.Redirect(logoutUrl);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get user information from Azure Easy Auth using server-side HttpClient with forwarded cookies
    /// </summary>
    /// <returns>Authenticated user or null</returns>
    private async Task<AuthenticatedUser?> GetEasyAuthUserAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext not available for authentication check");
            return null;
        }

        try
        {
            // Create request to /.auth/me with forwarded cookies
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var authMeUrl = $"{baseUrl}/.auth/me";

            var request = new HttpRequestMessage(HttpMethod.Get, authMeUrl);
            
            // Forward all cookies from the current request
            if (httpContext.Request.Headers.ContainsKey("Cookie"))
            {
                request.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
            }

            // Add cache control to ensure fresh data
            request.Headers.Add("Cache-Control", "no-cache");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Authentication check failed with status: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(responseContent) || responseContent.Trim() == "[]")
            {
                _logger.LogDebug("No authenticated user found");
                return null;
            }

            // Parse the Easy Auth response
            var userArray = JsonSerializer.Deserialize<EasyAuthUser[]>(responseContent);
            if (userArray == null || userArray.Length == 0)
            {
                _logger.LogDebug("Empty user array from Easy Auth");
                return null;
            }

            var userInfo = userArray[0];
            var provider = GetProviderFromIdentityProvider(userInfo.ProviderName);
            
            // Extract user details from claims
            var email = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Val ??
                       userInfo.UserId;
            
            var name = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "given_name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "preferred_username")?.Val ??
                      email;
                      
            // If name is still empty or same as email, try to extract from email
            if (string.IsNullOrEmpty(name) || name == email)
            {
                if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                {
                    name = email.Split('@')[0];
                }
                else
                {
                    name = "User";
                }
            }
            
            var profileImageUrl = GetProfileImageUrl(userInfo.ProviderName, userInfo.UserClaims);
                      
            var authenticatedUser = new AuthenticatedUser
            {
                UserId = userInfo.UserId,
                Email = email,
                Name = name,
                AuthProvider = provider,
                ProviderUserId = userInfo.UserId,
                ProfileImageUrl = profileImageUrl,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            // Auto-register user in the backend system (fire and forget)
            _ = Task.Run(async () => await AutoRegisterUserAsync());
            
            _logger.LogDebug("Successfully authenticated user: {Email}", email);
            return authenticatedUser;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during authentication check");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Easy Auth response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during authentication check");
            return null;
        }
    }

    /// <summary>
    /// Auto-register the current user in the backend system
    /// </summary>
    private async Task AutoRegisterUserAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userManagementService = scope.ServiceProvider.GetService<IUserManagementService>();
            
            if (userManagementService != null)
            {
                await userManagementService.RegisterCurrentUserAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-register user");
        }
    }

    /// <summary>
    /// Create a development user for testing
    /// </summary>
    /// <returns>Development user</returns>
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
    /// <param name="providerName">Provider name from Azure Easy Auth</param>
    /// <returns>Standardized provider name</returns>
    private static string GetProviderFromIdentityProvider(string providerName)
    {
        return providerName switch
        {
            "aad" => "Microsoft",
            "google" => "Google",
            "github" => "GitHub",
            "facebook" => "Facebook",
            "twitter" => "Twitter",
            _ => providerName
        };
    }

    /// <summary>
    /// Extract profile image URL from provider claims
    /// </summary>
    /// <param name="providerName">Provider name from Azure Easy Auth</param>
    /// <param name="claims">User claims</param>
    /// <returns>Profile image URL or null</returns>
    private static string? GetProfileImageUrl(string providerName, EasyAuthClaim[]? claims)
    {
        if (claims == null) return null;

        return providerName switch
        {
            "aad" => claims.FirstOrDefault(c => c.Typ == "picture")?.Val,
            "google" => claims.FirstOrDefault(c => c.Typ == "picture")?.Val,
            "github" => claims.FirstOrDefault(c => c.Typ == "urn:github:avatar_url")?.Val,
            "facebook" => claims.FirstOrDefault(c => c.Typ == "urn:facebook:picture")?.Val,
            "twitter" => claims.FirstOrDefault(c => c.Typ == "urn:twitter:profile_image_url_https")?.Val,
            _ => null
        };
    }
}
