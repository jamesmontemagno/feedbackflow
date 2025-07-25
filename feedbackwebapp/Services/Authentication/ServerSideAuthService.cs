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
    private readonly UserSettingsService _userSettingsService;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public ServerSideAuthService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<ServerSideAuthService> logger,
        UserSettingsService userSettingsService)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _serviceProvider = serviceProvider;
        _logger = logger;
        _userSettingsService = userSettingsService;
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
        
        // Build query parameters
        var queryParams = new List<string>();
        
        // Add redirect URL parameter
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            var encodedRedirect = Uri.EscapeDataString(redirectUrl);
            queryParams.Add($"post_login_redirect_url={encodedRedirect}");
        }
        else
        {
            // Default redirect to home page
            var defaultRedirect = Uri.EscapeDataString($"{baseUrl}/");
            queryParams.Add($"post_login_redirect_url={defaultRedirect}");
        }
        
        // Add Google-specific parameter for refresh tokens
        if (provider.ToLower() == "google")
        {
            queryParams.Add("access_type=offline");
        }
        
        // Append query parameters
        if (queryParams.Count > 0)
        {
            loginUrl += "?" + string.Join("&", queryParams);
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
        
        // Redirect to logout endpoint with post-logout redirect to home page
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var homeRedirect = Uri.EscapeDataString($"{baseUrl}/");
        var logoutUrl = $"{baseUrl}/.auth/logout?post_logout_redirect_uri={homeRedirect}";
        
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
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            
            // Check if we should refresh the token (if it's been over 12 hours since last login)
            var lastLogin = await _userSettingsService.GetLastLoginAtAsync();
            if (lastLogin.HasValue && DateTime.UtcNow.Subtract(lastLogin.Value).TotalHours > 12)
            {
                _logger.LogDebug("Last login was over 12 hours ago, attempting token refresh");
                await TryRefreshTokenAsync(baseUrl, httpContext);
            }
            
            // Create request to /.auth/me with forwarded cookies
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
            var email = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Val;
            
            var name = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "given_name")?.Val ??
                      userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "preferred_username")?.Val;
            
            // Provider-specific fallbacks for name
            if (string.IsNullOrEmpty(name))
            {
                name = provider switch
                {
                    "GitHub" => userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "urn:github:login")?.Val,
                    _ => null
                };
            }
                      
            // Final fallbacks for name
            if (string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                {
                    name = email.Split('@')[0];
                }
                else
                {
                    name = userInfo.UserId ?? "User";
                }
            }
            
            var profileImageUrl = GetProfileImageUrl(userInfo.ProviderName, userInfo.UserClaims);
                      
            var authenticatedUser = new AuthenticatedUser
            {
                UserId = userInfo.UserId ?? string.Empty,
                Email = email ?? string.Empty,
                Name = name,
                AuthProvider = provider,
                ProviderUserId = userInfo.UserId ?? string.Empty,
                ProfileImageUrl = profileImageUrl,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            
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
    /// Attempt to refresh the authentication token via /.auth/refresh
    /// </summary>
    /// <param name="baseUrl">Base URL of the application</param>
    /// <param name="httpContext">Current HTTP context</param>
    private async Task TryRefreshTokenAsync(string baseUrl, HttpContext httpContext)
    {
        try
        {
            var refreshUrl = $"{baseUrl}/.auth/refresh";
            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUrl);
            
            // Forward all cookies from the current request
            if (httpContext.Request.Headers.ContainsKey("Cookie"))
            {
                refreshRequest.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
            }

            // Add cache control to ensure fresh data
            refreshRequest.Headers.Add("Cache-Control", "no-cache");

            _logger.LogDebug("Attempting to refresh authentication token");
            var refreshResponse = await _httpClient.SendAsync(refreshRequest);

            if (refreshResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug("Token refresh successful");
                
                // Update last login time in user settings
                await _userSettingsService.UpdateLastLoginAtAsync();
            }
            else
            {
                _logger.LogDebug("Token refresh failed with status: {StatusCode}", refreshResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh attempt failed, but continuing with authentication check");
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
    /// Handle post-login user registration for OAuth providers (GitHub, Google, etc.)
    /// This should only be called after a user completes an OAuth login flow
    /// </summary>
    /// <returns>True if registration was successful, false otherwise</returns>
    public async Task<bool> HandlePostLoginRegistrationAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext not available for post-login registration");
                return false;
            }

            // Check if user is authenticated via OAuth providers (not password auth)
            var user = await GetEasyAuthUserAsync();
            if (user == null)
            {
                _logger.LogDebug("No authenticated user found for post-login registration");
                return false;
            }

            // Update last login time in user settings
            await _userSettingsService.UpdateLastLoginAtAsync();

            // Only auto-register for OAuth providers (GitHub, Google, Microsoft), not for password auth
            if (user.AuthProvider == "Password" || user.AuthProvider == "Development")
            {
                _logger.LogDebug("Skipping auto-registration for {AuthProvider} provider", user.AuthProvider);
                return true;
            }

            _logger.LogInformation("Performing post-login registration for user from {AuthProvider} provider", user.AuthProvider);

            // Perform registration and await the result
            await AutoRegisterUserAsync();
            
            _logger.LogInformation("Post-login registration completed successfully for {AuthProvider} user", user.AuthProvider);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during post-login registration");
            return false;
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

/// <summary>
/// Represents the Easy Auth user information from /.auth/me endpoint
/// </summary>
internal class EasyAuthUser
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
    
    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;
    
    [JsonPropertyName("user_claims")]
    public EasyAuthClaim[]? UserClaims { get; set; }
    
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("expires_on")]
    public string? ExpiresOn { get; set; }
    
    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }
}

/// <summary>
/// Represents a claim from Easy Auth
/// </summary>
internal class EasyAuthClaim
{
    [JsonPropertyName("typ")]
    public string Typ { get; set; } = string.Empty;
    
    [JsonPropertyName("val")]
    public string? Val { get; set; }
}
