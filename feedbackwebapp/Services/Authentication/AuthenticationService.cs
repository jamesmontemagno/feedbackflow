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
    private readonly UserSettingsService _userSettingsService;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;
    private string? _lastAuthenticationError;

    public event EventHandler<bool>? AuthenticationStateChanged;

    /// <summary>
    /// Gets the last authentication error message, if any
    /// </summary>
    public string? LastAuthenticationError => _lastAuthenticationError;

    public AuthenticationService(IJSRuntime jsRuntime, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider, IServiceProvider serviceProvider, UserSettingsService userSettingsService)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _dataProtector = dataProtectionProvider.CreateProtector("FeedbackFlow.Authentication.v1");
        _serviceProvider = serviceProvider;
        _userSettingsService = userSettingsService;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        await _userSettingsService.LogAuthDebugAsync("Local debug auth: IsAuthenticatedAsync called");
        
        // Check if we're in local debug mode - if so, assume authenticated
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";
        
        if (bypassAuth && isDevelopment)
        {
            await _userSettingsService.LogAuthDebugAsync("Local debug auth: Bypassing authentication for development");
            _isAuthenticated = true;
            return true;
        }

        // Fast-track check: if user hasn't even attempted to log in, skip expensive checks
        var hasLoginAttempt = await _userSettingsService.GetLoginAttemptAsync();
        if (!hasLoginAttempt)
        {
            await _userSettingsService.LogAuthDebugAsync("No login attempt detected - user has not tried to log in");
            _isAuthenticated = false;
            return false;
        }

        // For non-development, this service shouldn't be used anymore
        // but we'll keep the logic for backward compatibility
        if (_isAuthenticated.HasValue)
        {
            await _userSettingsService.LogAuthDebugAsync("Password auth: Returning cached result", new { isAuthenticated = _isAuthenticated.Value });
            return _isAuthenticated.Value;
        }

        var protectedToken = await _userSettingsService.GetStringFromLocalStorageAsync(AUTH_KEY);
        if (string.IsNullOrEmpty(protectedToken))
        {
            await _userSettingsService.LogAuthDebugAsync("Password auth: No token found");
            _isAuthenticated = false;
            return false;
        }

        try
        {
            // Try to unprotect the token - if successful, user is authenticated
            var unprotectedData = _dataProtector.Unprotect(protectedToken);
            
            // Verify the unprotected data contains valid authentication marker
            _isAuthenticated = unprotectedData == "FeedbackFlow_Authenticated";
            await _userSettingsService.LogAuthDebugAsync("Password auth: Token validation result", new { isAuthenticated = _isAuthenticated.Value });
            return _isAuthenticated.Value;
        }
        catch (CryptographicException)
        {
            await _userSettingsService.LogAuthErrorAsync("Password auth: Token is invalid or tampered with");
            // Token is invalid or tampered with
            _isAuthenticated = false;
            await _userSettingsService.RemoveFromLocalStorageAsync(AUTH_KEY);
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string password)
    {
        await _userSettingsService.LogAuthDebugAsync("Password auth: AuthenticateAsync called");
        
        if (string.IsNullOrEmpty(password))
        {
            await _userSettingsService.LogAuthWarnAsync("Password auth: Empty password provided");
            _isAuthenticated = false;
            return false;
        }

        // Get the configured password
        var configuredPassword = _configuration["FeedbackApp:AccessPassword"];
        if (string.IsNullOrEmpty(configuredPassword))
        {
            await _userSettingsService.LogAuthErrorAsync("Password auth: No configured password found");
            _isAuthenticated = false;
            return false;
        }

        var isValid = password == configuredPassword;
        await _userSettingsService.LogAuthDebugAsync("Password auth: Password validation result", new { isValid });
        
        if (isValid)
        {
            // Clear any previous error
            _lastAuthenticationError = null;

            // Create a protected token that proves authentication
            var protectedToken = _dataProtector.Protect("FeedbackFlow_Authenticated");
            await _userSettingsService.SaveStringToLocalStorageAsync(AUTH_KEY, protectedToken);
            _isAuthenticated = true;

            // Try to register user in the backend system - this must succeed
            var registrationResult = await AutoRegisterUserAsync();
            if (!registrationResult.Success)
            {
                await _userSettingsService.LogAuthErrorAsync("Password auth: User registration failed", registrationResult.ErrorMessage);
                // Registration failed - log user out and clear authentication
                await SetAuthenticatedAsync(false);
                _isAuthenticated = false;
                _lastAuthenticationError = registrationResult.ErrorMessage ?? "User registration failed. The system may be temporarily unavailable.";
                AuthenticationStateChanged?.Invoke(this, false);
                
                // Return false to indicate authentication failure due to system unavailability
                return false;
            }

            await _userSettingsService.LogAuthDebugAsync("Password auth: Authentication successful");
            AuthenticationStateChanged?.Invoke(this, true);
        }
        else
        {
            await _userSettingsService.LogAuthWarnAsync("Password auth: Invalid password");
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
            await _userSettingsService.RemoveFromLocalStorageAsync(AUTH_KEY);
        }
        _isAuthenticated = authenticated;
    }

    public async Task LogoutAsync()
    {
        await SetAuthenticatedAsync(false);
        await _userSettingsService.ClearLoginAttemptAsync(); // Clear login attempt flag
        _lastAuthenticationError = null; // Clear any authentication errors on logout
        AuthenticationStateChanged?.Invoke(this, false);
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        // Check if we're in local debug mode first
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";
        
        if (bypassAuth && isDevelopment)
        {
            // Return a debug user for local development
            return new AuthenticatedUser
            {
                UserId = "debug-user-12345",
                Email = "debug@feedbackflow.local",
                Name = "Debug User",
                AuthProvider = "Debug",
                ProviderUserId = "debug-user-local",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
        }

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

    /// <inheritdoc />
    public Task<bool> HandlePostLoginRegistrationAsync()
    {
        // Password auth handles registration during AuthenticateAsync, not post-login
        // This is already handled in the AuthenticateAsync method
        return Task.FromResult(true);
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