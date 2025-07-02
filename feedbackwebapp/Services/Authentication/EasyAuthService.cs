using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using SharedDump.Models.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Azure Easy Auth implementation of authentication service
/// </summary>
public class EasyAuthService : IAuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly NavigationManager _navigationManager;
    private const string AUTH_USER_KEY = "feedbackflow_easyauth_user";
    private AuthenticatedUser? _currentUser;
    private bool? _isAuthenticated;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public EasyAuthService(
        IJSRuntime jsRuntime, 
        IConfiguration configuration, 
        NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _navigationManager = navigationManager;
    }

    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        // Check if auth is bypassed for development
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

        if (bypassAuth && isDevelopment)
        {
            _isAuthenticated = true;
            _currentUser = CreateDevelopmentUser();
            return true;
        }

        try
        {
            // In Azure Easy Auth, check for user info endpoint
            var userInfo = await GetEasyAuthUserAsync();
            _isAuthenticated = userInfo != null;
            _currentUser = userInfo;
            return _isAuthenticated.Value;
        }
        catch
        {
            _isAuthenticated = false;
            _currentUser = null;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        var isAuthenticated = await IsAuthenticatedAsync();
        return isAuthenticated ? _currentUser : null;
    }

    /// <inheritdoc />
    public string GetLoginUrl(string provider, string? redirectUrl = null)
    {
        var baseUrl = _navigationManager.BaseUri.TrimEnd('/');
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
        try
        {
            // Clear cached user info
            _currentUser = null;
            _isAuthenticated = false;
            
            // Clear any stored user data
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            
            // Trigger authentication state change
            AuthenticationStateChanged?.Invoke(this, false);
            
            // Navigate to logout endpoint
            var baseUrl = _navigationManager.BaseUri.TrimEnd('/');
            var logoutUrl = $"{baseUrl}/.auth/logout";
            _navigationManager.NavigateTo(logoutUrl, forceLoad: true);
        }
        catch (Exception)
        {
            // If logout fails, still clear local state
            _currentUser = null;
            _isAuthenticated = false;
            AuthenticationStateChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Get user information from Azure Easy Auth
    /// </summary>
    /// <returns>Authenticated user or null</returns>
    private async Task<AuthenticatedUser?> GetEasyAuthUserAsync()
    {
        try
        {
            // First check if we have cached user data in localStorage
            var cachedUserJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AUTH_USER_KEY);
            if (!string.IsNullOrEmpty(cachedUserJson))
            {
                try
                {
                    var cachedUser = JsonSerializer.Deserialize<AuthenticatedUser>(cachedUserJson);
                    if (cachedUser != null)
                    {
                        // Return cached user, but still validate with Easy Auth in background
                        _ = Task.Run(async () => await ValidateEasyAuthAsync());
                        return cachedUser;
                    }
                }
                catch
                {
                    // Invalid cached data, remove it
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                }
            }

            // Try to get user info from /.auth/me endpoint
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Set a reasonable timeout
            
            var baseUrl = _navigationManager.BaseUri.TrimEnd('/');
            var response = await httpClient.GetAsync($"{baseUrl}/.auth/me");
            
            if (!response.IsSuccessStatusCode)
            {
                // If we get 401/403, user is not authenticated
                // Clear any cached data
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content) || content.Trim() == "[]")
            {
                // Empty array means no authenticated user
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                return null;
            }
            
            // Parse the Easy Auth response
            var authMeResponse = JsonSerializer.Deserialize<EasyAuthUser[]>(content);
            if (authMeResponse == null || authMeResponse.Length == 0)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                return null;
            }
            
            var userInfo = authMeResponse[0];
            var provider = GetProviderFromIdentityProvider(userInfo.IdentityProvider);
            
            // Extract user details from claims
            var email = userInfo.UserDetails;
            var name = userInfo.Claims?.FirstOrDefault(c => c.Typ == "name")?.Val ??
                      userInfo.Claims?.FirstOrDefault(c => c.Typ == "given_name")?.Val ??
                      email;
            var profileImageUrl = GetProfileImageUrl(userInfo.IdentityProvider, userInfo.Claims);
                      
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

            // Store user data in localStorage for faster subsequent loads
            var userJson = JsonSerializer.Serialize(authenticatedUser);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_USER_KEY, userJson);
            
            return authenticatedUser;
        }
        catch (HttpRequestException)
        {
            // Network error or Easy Auth not available
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            return null;
        }
        catch (TaskCanceledException)
        {
            // Timeout
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            return null;
        }
        catch (Exception)
        {
            // Any other error - Easy Auth not configured or not working
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            return null;
        }
    }

    /// <summary>
    /// Validate Easy Auth status in background (for cached users)
    /// </summary>
    private async Task ValidateEasyAuthAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            
            var baseUrl = _navigationManager.BaseUri.TrimEnd('/');
            var response = await httpClient.GetAsync($"{baseUrl}/.auth/me");
            
            if (!response.IsSuccessStatusCode)
            {
                // User is no longer authenticated, clear cache and reset state
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                _isAuthenticated = false;
                _currentUser = null;
                AuthenticationStateChanged?.Invoke(this, false);
            }
        }
        catch
        {
            // If validation fails, we'll catch it on the next regular check
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
    /// Reset authentication state to force a fresh check
    /// </summary>
    internal async Task ResetAuthenticationStateAsync()
    {
        _isAuthenticated = null;
        _currentUser = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
    }

    /// <summary>
    /// Reset authentication state to force a fresh check (synchronous version for reflection)
    /// </summary>
    internal void ResetAuthenticationState()
    {
        _isAuthenticated = null;
        _currentUser = null;
        // Note: Cannot clear localStorage synchronously, will be cleared on next auth check
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
            _ => identityProvider
        };
    }

    /// <summary>
    /// Extract profile image URL from provider claims
    /// </summary>
    /// <param name="identityProvider">Identity provider from Azure Easy Auth</param>
    /// <param name="claims">User claims</param>
    /// <returns>Profile image URL or null</returns>
    private static string? GetProfileImageUrl(string identityProvider, EasyAuthClaim[]? claims)
    {
        if (claims == null) return null;

        return identityProvider switch
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
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("userDetails")]
    public string UserDetails { get; set; } = string.Empty;
    
    [JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; } = string.Empty;
    
    [JsonPropertyName("claims")]
    public EasyAuthClaim[]? Claims { get; set; }
}

/// <summary>
/// Represents a claim from Easy Auth
/// </summary>
internal class EasyAuthClaim
{
    [JsonPropertyName("typ")]
    public string Typ { get; set; } = string.Empty;
    
    [JsonPropertyName("val")]
    public string Val { get; set; } = string.Empty;
}