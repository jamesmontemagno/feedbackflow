using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Service to manage automatic authentication token refresh and state monitoring
/// </summary>
public interface IAuthTokenRefreshService
{
    /// <summary>
    /// Initialize the token refresh service
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Start automatic token refresh
    /// </summary>
    Task StartAutoRefreshAsync();

    /// <summary>
    /// Stop automatic token refresh
    /// </summary>
    Task StopAutoRefreshAsync();

    /// <summary>
    /// Manually trigger a token refresh
    /// </summary>
    Task ManualRefreshAsync();

    /// <summary>
    /// Event triggered when authentication state changes due to token refresh
    /// </summary>
    event EventHandler<bool>? AuthenticationStateChanged;
}

/// <summary>
/// Implementation of the token refresh service
/// </summary>
public class AuthTokenRefreshService : IAuthTokenRefreshService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthTokenRefreshService> _logger;
    private readonly IAuthenticationService _authService;
    private readonly IConfiguration _configuration;
    private DotNetObjectReference<AuthTokenRefreshService>? _dotNetReference;
    private bool _isInitialized = false;

    /// <inheritdoc />
    public event EventHandler<bool>? AuthenticationStateChanged;

    public AuthTokenRefreshService(
        IJSRuntime jsRuntime, 
        ILogger<AuthTokenRefreshService> logger,
        IAuthenticationService authService,
        IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _authService = authService;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _dotNetReference = DotNetObjectReference.Create(this);
            
            // Get configuration for token refresh intervals
            var refreshIntervalMinutes = _configuration.GetValue("Authentication:TokenRefreshIntervalMinutes", 15);
            var minRefreshIntervalMinutes = _configuration.GetValue("Authentication:MinTokenRefreshIntervalMinutes", 5);
            
            // Set up the .NET helper for JavaScript callbacks
            await _jsRuntime.InvokeVoidAsync("authTokenManager.setDotNetHelper", _dotNetReference);
            
            // Configure refresh intervals
            await _jsRuntime.InvokeVoidAsync("authTokenManager.setRefreshInterval", refreshIntervalMinutes * 60 * 1000);
            await _jsRuntime.InvokeVoidAsync("authTokenManager.setMinRefreshInterval", minRefreshIntervalMinutes * 60 * 1000);
            
            _isInitialized = true;
            _logger.LogInformation("AuthTokenRefreshService initialized with refresh interval: {RefreshInterval} minutes, min interval: {MinInterval} minutes", 
                refreshIntervalMinutes, minRefreshIntervalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AuthTokenRefreshService");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StartAutoRefreshAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("authTokenManager.startAutoRefresh");
            _logger.LogInformation("Auto token refresh started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start auto token refresh");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopAutoRefreshAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("authTokenManager.stopAutoRefresh");
            _logger.LogInformation("Auto token refresh stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop auto token refresh");
        }
    }

    /// <inheritdoc />
    public async Task ManualRefreshAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("authTokenManager.manualRefresh");
            _logger.LogInformation("Manual token refresh triggered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger manual token refresh");
            throw;
        }
    }

    /// <summary>
    /// Called from JavaScript when authentication state changes
    /// </summary>
    /// <param name="isAuthenticated">Whether the user is authenticated</param>
    [JSInvokable]
    public async Task OnAuthStateChangedFromJS(bool isAuthenticated)
    {
        try
        {
            _logger.LogInformation("Authentication state changed from JavaScript: {IsAuthenticated}", isAuthenticated);
            
            // Verify the state with the server-side authentication service
            var serverSideAuth = await _authService.IsAuthenticatedAsync();
            
            if (serverSideAuth != isAuthenticated)
            {
                _logger.LogWarning("Authentication state mismatch - JS: {JSAuth}, Server: {ServerAuth}", 
                    isAuthenticated, serverSideAuth);
            }

            // Use the server-side state as the source of truth
            AuthenticationStateChanged?.Invoke(this, serverSideAuth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling authentication state change from JavaScript");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAutoRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping auto refresh during disposal");
        }

        _dotNetReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}
