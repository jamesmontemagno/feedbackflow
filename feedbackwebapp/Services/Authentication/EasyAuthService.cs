using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using SharedDump.Models.Authentication;
using System.Text.Json;
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
            "microsoft" => "microsoftaccount",
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
            // For now, let's use a simpler check - just verify that auth bypass is not enabled
            // In a real implementation, this would call /.auth/me endpoint
            
            // TODO: Implement proper Azure Easy Auth user info retrieval
            // This would typically involve:
            // 1. Making a HTTP request to /.auth/me endpoint
            // 2. Parsing the returned user information
            // 3. Creating AuthenticatedUser from the response
            
            // For now, return null to indicate not authenticated via Easy Auth
            return null;
        }
        catch (Exception)
        {
            // If we can't get user info from Easy Auth, user is not authenticated
            return null;
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