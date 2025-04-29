using Microsoft.JSInterop;

namespace FeedbackWebApp.Services.Authentication;

public class AuthenticationService
{
    private readonly IJSRuntime _jsRuntime;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;

    public AuthenticationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AUTH_KEY);
        _isAuthenticated = !string.IsNullOrEmpty(token);
        return _isAuthenticated.Value;
    }

    public async Task SetAuthenticatedAsync(bool authenticated)
    {
        if (authenticated)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AUTH_KEY, "authenticated");
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AUTH_KEY);
        }
        _isAuthenticated = authenticated;
    }

    public async Task LogoutAsync()
    {
        await SetAuthenticatedAsync(false);
    }
}