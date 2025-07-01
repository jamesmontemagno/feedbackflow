using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using SharedDump.Models.Authentication;

namespace FeedbackWebApp.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly IDataProtector _dataProtector;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;

    public event EventHandler<bool>? AuthenticationStateChanged;

    public AuthenticationService(IJSRuntime jsRuntime, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _dataProtector = dataProtectionProvider.CreateProtector("FeedbackFlow.Authentication.v1");
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        var protectedToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AUTH_KEY);
        if (string.IsNullOrEmpty(protectedToken))
        {
            _isAuthenticated = false;
            return false;
        }

        try
        {
            // Try to unprotect the token - if successful, user is authenticated
            var unprotectedData = _dataProtector.Unprotect(protectedToken);
            
            // Verify the unprotected data contains valid authentication marker
            _isAuthenticated = unprotectedData == "FeedbackFlow_Authenticated";
            return _isAuthenticated.Value;
        }
        catch (CryptographicException)
        {
            // Token is invalid or tampered with
            _isAuthenticated = false;
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_KEY);
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            _isAuthenticated = false;
            return false;
        }

        // Get the configured password
        var configuredPassword = _configuration["FeedbackApp:AccessPassword"];
        if (string.IsNullOrEmpty(configuredPassword))
        {
            _isAuthenticated = false;
            return false;
        }

        var isValid = password == configuredPassword;
        if (isValid)
        {
            // Create a protected token that proves authentication
            var protectedToken = _dataProtector.Protect("FeedbackFlow_Authenticated");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_KEY, protectedToken);
            _isAuthenticated = true;
            AuthenticationStateChanged?.Invoke(this, true);
        }
        else
        {
            _isAuthenticated = false;
            AuthenticationStateChanged?.Invoke(this, false);
        }

        return isValid;
    }

    public async Task SetAuthenticatedAsync(bool authenticated)
    {
        if (!authenticated)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_KEY);
        }
        _isAuthenticated = authenticated;
    }

    public async Task LogoutAsync()
    {
        await SetAuthenticatedAsync(false);
        AuthenticationStateChanged?.Invoke(this, false);
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
            return null;

        // For password auth, we don't have user details, so return a basic user
        return new AuthenticatedUser
        {
            UserId = "password-user",
            Email = "password@local.dev",
            Name = "Password User",
            AuthProvider = "Password",
            ProviderUserId = "password-user",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }

    public string GetLoginUrl(string provider, string? redirectUrl = null)
    {
        // Password auth doesn't use login URLs
        return "/";
    }
}