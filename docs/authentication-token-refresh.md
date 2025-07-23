# Authentication Token Refresh Enhancement

## Overview

I've implemented an automatic authentication token refresh system to prevent users from being logged out due to expired tokens. The system periodically refreshes Azure Easy Auth tokens in the background and notifies the UI when authentication state changes.

## What's New

### 1. Automatic Token Refresh
- **JavaScript TokenManager**: Automatically refreshes tokens every 15 minutes (configurable)
- **Background Processing**: Runs silently without interrupting user workflow
- **Smart Refresh Logic**: Avoids excessive refresh calls with minimum interval protection

### 2. Blazor Integration
- **AuthTokenRefreshService**: C# service that interfaces with JavaScript token manager
- **State Synchronization**: Automatically updates UI components when auth state changes
- **Event-Driven Updates**: UserNavigation and other components automatically refresh

### 3. Configuration Options
```json
{
  "Authentication": {
    "UseEasyAuth": false,
    "BypassInDevelopment": true,
    "TokenRefreshIntervalMinutes": 15,    // How often to refresh (default: 15 min)
    "MinTokenRefreshIntervalMinutes": 5   // Minimum time between refreshes (default: 5 min)
  }
}
```

## How It Works

### Automatic Refresh Process
1. **Initialization**: Token refresh starts when the MainLayout loads
2. **Periodic Checks**: JavaScript timer triggers refresh every configured interval
3. **Smart Refresh**: Calls `/.auth/refresh` endpoint to renew tokens
4. **State Verification**: Checks `/.auth/me` to verify authentication status
5. **UI Updates**: Notifies Blazor components when auth state changes

### Manual Refresh Options
- **JavaScript**: `await authTokenManager.manualRefresh()`
- **C#**: `await TokenRefreshService.ManualRefreshAsync()`
- **Helper Function**: `await ensureAuthenticated()` - guarantees fresh auth before API calls

### Authentication State Monitoring
The system monitors authentication through multiple layers:
- **JavaScript**: Direct browser cookie/session monitoring
- **Server-side**: Validates through HttpContext and Azure Easy Auth
- **Event Notifications**: Real-time updates to UI components

## Benefits

### 1. Improved User Experience
- No more unexpected logouts during active sessions
- Seamless background token renewal
- Automatic UI updates when auth state changes

### 2. Better Reliability
- Proactive token refresh before expiration
- Fallback authentication checks
- Graceful handling of refresh failures

### 3. Configurable Behavior
- Adjustable refresh intervals
- Environment-specific settings
- Debug logging for troubleshooting

## Usage Examples

### For Component Developers
```csharp
@inject IAuthTokenRefreshService TokenRefreshService

// Subscribe to auth state changes
protected override async Task OnInitializedAsync()
{
    TokenRefreshService.AuthenticationStateChanged += OnAuthStateChanged;
}

private void OnAuthStateChanged(object? sender, bool isAuthenticated)
{
    InvokeAsync(StateHasChanged);
}
```

### For API Calls
```javascript
// Ensure authentication before critical API calls
if (await ensureAuthenticated()) {
    // Make authenticated API call
    const response = await fetch('/api/protected-endpoint');
} else {
    // Handle authentication failure
    console.warn('User not authenticated');
}
```

### Manual Refresh Trigger
```csharp
// Force immediate token refresh
await TokenRefreshService.ManualRefreshAsync();
```

## Troubleshooting

### Common Issues
1. **Token refresh failures**: Check Azure Easy Auth configuration
2. **Excessive refresh calls**: Verify minimum interval settings
3. **UI not updating**: Ensure components subscribe to AuthenticationStateChanged events

### Debug Information
- Browser console shows detailed refresh activity
- Server logs include authentication state changes
- Configuration values logged at startup

### Configuration Tips
- **Development**: Use shorter intervals for testing (2-5 minutes)
- **Production**: Standard intervals (15-30 minutes) for efficiency
- **High-traffic**: Increase minimum interval to reduce server load

## Future Enhancements

### Potential Improvements
1. **Token Expiration Detection**: Parse token expiry and refresh proactively
2. **Offline Handling**: Detect network issues and retry logic
3. **Session Recovery**: Attempt to restore expired sessions
4. **Analytics**: Track refresh success rates and timing

### Integration Opportunities
1. **SignalR**: Real-time auth state broadcasting
2. **Background Services**: Server-side token monitoring
3. **Health Checks**: Include auth health in application monitoring

This enhancement significantly improves the authentication reliability of the FeedbackFlow application while maintaining backward compatibility with existing authentication patterns.
