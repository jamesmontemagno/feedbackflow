using Microsoft.JSInterop;

namespace FeedbackWebApp.Services.Authentication;

public class AuthenticationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private const string AUTH_KEY = "feedbackflow_auth";
    private bool? _isAuthenticated;
    private bool _disposed;
    private IJSObjectReference? _jsModule;

    public AuthenticationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        if (_isAuthenticated.HasValue)
            return _isAuthenticated.Value;

        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, AUTH_KEY);
        _isAuthenticated = !string.IsNullOrEmpty(token);
        return _isAuthenticated.Value;
    }

    public async Task SetAuthenticatedAsync(bool authenticated, CancellationToken cancellationToken = default)
    {
        if (authenticated)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, AUTH_KEY, "authenticated");
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, AUTH_KEY);
        }
        _isAuthenticated = authenticated;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await SetAuthenticatedAsync(false, cancellationToken);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_jsModule != null)
            {
                try
                {
                    await _jsModule.DisposeAsync();
                }
                catch
                {
                    // Ignore errors during disposal
                }
                _jsModule = null;
            }
            
            _disposed = true;
        }
    }
}