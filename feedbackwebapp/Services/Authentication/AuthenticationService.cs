using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using SharedDump.Models.Authentication;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly IDataProtector _dataProtector;
    private readonly IServiceProvider _serviceProvider;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;
    private string? _lastAuthenticationError;

    public event EventHandler<bool>? AuthenticationStateChanged;

    /// <summary>
    /// Gets the last authentication error message, if any
    /// </summary>
    public string? LastAuthenticationError => _lastAuthenticationError;

    public AuthenticationService(IJSRuntime jsRuntime, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider, IServiceProvider serviceProvider)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _dataProtector = dataProtectionProvider.CreateProtector("FeedbackFlow.Authentication.v1");
        _serviceProvider = serviceProvider;
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
            // Clear any previous error
            _lastAuthenticationError = null;

            // Create a protected token that proves authentication
            var protectedToken = _dataProtector.Protect("FeedbackFlow_Authenticated");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_KEY, protectedToken);
            _isAuthenticated = true;

            // Try to register user in the backend system - this must succeed
            var registrationResult = await AutoRegisterUserAsync();
            if (!registrationResult.Success)
            {
                // Registration failed - log user out and clear authentication
                await SetAuthenticatedAsync(false);
                _isAuthenticated = false;
                _lastAuthenticationError = registrationResult.ErrorMessage ?? "User registration failed. The system may be temporarily unavailable.";
                AuthenticationStateChanged?.Invoke(this, false);
                
                // Return false to indicate authentication failure due to system unavailability
                return false;
            }

            AuthenticationStateChanged?.Invoke(this, true);
        }
        else
        {
            _isAuthenticated = false;
            _lastAuthenticationError = "Invalid password";
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
        _lastAuthenticationError = null; // Clear any authentication errors on logout
        AuthenticationStateChanged?.Invoke(this, false);
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        var isAuthenticated = await IsAuthenticatedAsync();
        if (!isAuthenticated)
            return null;

        // For password auth, create a consistent user that matches what the backend expects
        // This user will be registered in the backend when they first authenticate
        return new AuthenticatedUser
        {
            UserId = "password-user", // This will be updated by the backend when registered
            Email = "password@feedbackflow.local",
            Name = "FeedbackFlow User",
            AuthProvider = "Password",
            ProviderUserId = "password-user-local", // Unique identifier for password users
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }

    public string GetLoginUrl(string provider, string? redirectUrl = null)
    {
        // Password auth doesn't use login URLs
        return "/";
    }

    /// <summary>
    /// Auto-register the current user in the backend system
    /// </summary>
    /// <returns>Result indicating success or failure with error details</returns>
    private async Task<AuthenticationResult> AutoRegisterUserAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userManagementService = scope.ServiceProvider.GetService<IUserManagementService>();
            
            if (userManagementService == null)
            {
                return new AuthenticationResult 
                { 
                    Success = false, 
                    ErrorMessage = "User management service not available" 
                };
            }

            var registrationResult = await userManagementService.RegisterCurrentUserAsync();
            
            if (!registrationResult.Success)
            {
                return new AuthenticationResult 
                { 
                    Success = false, 
                    ErrorMessage = $"User registration failed: {registrationResult.ErrorMessage}" 
                };
            }

            return new AuthenticationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new AuthenticationResult 
            { 
                Success = false, 
                ErrorMessage = $"Registration error: {ex.Message}" 
            };
        }
    }
}

/// <summary>
/// Result of an authentication operation
/// </summary>
public class AuthenticationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}