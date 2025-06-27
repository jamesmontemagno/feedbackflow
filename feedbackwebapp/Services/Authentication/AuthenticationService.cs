using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace FeedbackWebApp.Services.Authentication;

public class AuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;

    public AuthenticationService(IJSRuntime jsRuntime, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        var storedHash = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AUTH_KEY);
        if (string.IsNullOrEmpty(storedHash))
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

        // Verify the stored hash matches the configured password hash
        var expectedHash = ComputePasswordHash(configuredPassword);
        _isAuthenticated = storedHash == expectedHash;
        return _isAuthenticated.Value;
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
            var passwordHash = ComputePasswordHash(password);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_KEY, passwordHash);
            _isAuthenticated = true;
        }
        else
        {
            _isAuthenticated = false;
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
    }

    private static string ComputePasswordHash(string password)
    {
        using var sha256 = SHA256.Create();
        var saltedPassword = $"FeedbackFlow_{password}_Salt2025";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hash);
    }
}