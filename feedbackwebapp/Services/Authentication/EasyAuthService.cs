using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using SharedDump.Models.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Azure Easy Auth implementation of authentication service
/// </summary>
public class EasyAuthService : IAuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly NavigationManager _navigationManager;
    private readonly IServiceProvider _serviceProvider;
    private const string AUTH_USER_KEY = "feedbackflow_easyauth_user";
    private AuthenticatedUser? _currentUser;
    private bool? _isAuthenticated;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public EasyAuthService(
        IJSRuntime jsRuntime, 
        IConfiguration configuration, 
        NavigationManager navigationManager,
        IServiceProvider serviceProvider)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _navigationManager = navigationManager;
        _serviceProvider = serviceProvider;
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
            // Note: We can't use HttpClient from Blazor Server to call /.auth/me because 
            // it won't include the authentication cookies. Instead, we'll use JS interop.
            return await GetEasyAuthUserViaJSAsync();
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
    /// Get user information from Azure Easy Auth using JavaScript fetch to include cookies
    /// </summary>
    /// <returns>Authenticated user or null</returns>
    private async Task<AuthenticatedUser?> GetEasyAuthUserViaJSAsync()
    {
        try
        {
            // Ensure JavaScript function is available with retry logic
            var maxAttempts = 3;
            var functionExists = false;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    functionExists = await _jsRuntime.InvokeAsync<bool>("eval", "typeof fetchAuthMe === 'function'");
                    if (functionExists) break;
                    
                    // Wait a bit before retrying
                    await Task.Delay(100);
                }
                catch (JSException)
                {
                    if (attempt == maxAttempts - 1) throw;
                    await Task.Delay(100);
                }
            }
            
            if (!functionExists)
            {
                throw new InvalidOperationException("fetchAuthMe JavaScript function is not available after retries");
            }

            // Use JavaScript fetch to call /.auth/me with credentials included
            var authMeResponse = await _jsRuntime.InvokeAsync<string>("fetchAuthMe");
            
            if (string.IsNullOrEmpty(authMeResponse) || authMeResponse.Trim() == "[]")
            {
                // Empty array means no authenticated user
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                return null;
            }
            
            // Parse the Easy Auth response
            var userArray = JsonSerializer.Deserialize<EasyAuthUser[]>(authMeResponse);
            if (userArray == null || userArray.Length == 0)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
                return null;
            }
            
            var userInfo = userArray[0];
            var provider = GetProviderFromIdentityProvider(userInfo.ProviderName);
            
            // Extract user details from claims - be more thorough with name extraction
            var email = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Val ??
                       userInfo.UserId; // Fallback to user_id if no email claim
            
            // Try multiple claim types for name
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
                    name = "User"; // Fallback
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

            // Store user data in localStorage for faster subsequent loads
            var userJson = JsonSerializer.Serialize(authenticatedUser);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_USER_KEY, userJson);
            
            // Auto-register user in the backend system (fire and forget)
            _ = Task.Run(async () => await AutoRegisterUserAsync());
            
            // Also store debug info to help with troubleshooting (temporary)
            try
            {
                var debugInfo = new
                {
                    RawUserInfo = userInfo,
                    ParsedUser = authenticatedUser,
                    AllClaims = userInfo.UserClaims?.Select(c => new { c.Typ, c.Val }).ToArray(),
                    Timestamp = DateTime.UtcNow
                };
                var debugJson = JsonSerializer.Serialize(debugInfo, new JsonSerializerOptions { WriteIndented = true });
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "feedbackflow_debug_claims", debugJson);
            }
            catch
            {
                // Ignore debug info errors
            }
            
            return authenticatedUser;
        }
        catch (JsonException jsonEx)
        {
            // JSON parsing error - likely invalid response format
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            throw new InvalidOperationException($"Failed to parse Easy Auth response: {jsonEx.Message}", jsonEx);
        }
        catch (JSException jsEx)
        {
            // JavaScript interop error
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            throw new InvalidOperationException($"JavaScript interop error calling fetchAuthMe: {jsEx.Message}", jsEx);
        }
        catch (Exception ex)
        {
            // Any other error with JS interop or JSON parsing
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_USER_KEY);
            throw new InvalidOperationException($"Unexpected error getting Easy Auth user: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate Easy Auth status in background (for cached users)
    /// </summary>
    private async Task ValidateEasyAuthAsync()
    {
        try
        {
            // Use JavaScript fetch to validate auth status with credentials
            var authResponse = await _jsRuntime.InvokeAsync<string?>("fetchAuthMe");
            
            if (string.IsNullOrEmpty(authResponse) || authResponse.Trim() == "[]")
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
    /// Auto-register the current user in the backend system
    /// </summary>
    private async Task AutoRegisterUserAsync()
    {
        try
        {
            // Use scoped service to avoid circular dependency
            using var scope = _serviceProvider.CreateScope();
            var userManagementService = scope.ServiceProvider.GetService<IUserManagementService>();
            
            if (userManagementService != null)
            {
                await userManagementService.RegisterCurrentUserAsync();
            }
        }
        catch (Exception)
        {
            // Silently ignore registration errors to not impact user experience
            // User registration can be retried later if needed
        }
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
    public string UserId { get; set; } = string.Empty;
    
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
    public string Val { get; set; } = string.Empty;
}