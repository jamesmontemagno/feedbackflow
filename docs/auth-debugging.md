# Authentication Debugging

The FeedbackFlow web application includes built-in authentication debugging capabilities that log authentication-specific events to the browser console for development and troubleshooting purposes.

## Configuration

### Enable Auth Debugging

Add the following configuration to enable authentication debugging:

**appsettings.json** (Production - disabled by default):
```json
{
  "Authentication": {
    "DEBUG": false
  }
}
```

**appsettings.Development.json** (Development - enabled by default):
```json
{
  "Authentication": {
    "DEBUG": true
  }
}
```

### Environment Variable
You can also set this via environment variable:
```bash
Authentication__DEBUG=true
```

## What Gets Logged

When `Authentication:DEBUG` is enabled, the following authentication events are logged to the browser console:

### Password Authentication Service
- `[AUTH DEBUG] Password auth: IsAuthenticatedAsync called` - When checking authentication status
- `[AUTH DEBUG] Password auth: Returning cached result` - When using cached authentication state
- `[AUTH DEBUG] Password auth: No token found` - When no authentication token is found
- `[AUTH DEBUG] Password auth: Token validation result` - Result of token validation
- `[AUTH ERROR] Password auth: Token is invalid or tampered with` - When token validation fails
- `[AUTH DEBUG] Password auth: AuthenticateAsync called` - When attempting to authenticate
- `[AUTH WARN] Password auth: Empty password provided` - When empty password is submitted
- `[AUTH ERROR] Password auth: No configured password found` - When no password is configured
- `[AUTH DEBUG] Password auth: Password validation result` - Result of password validation
- `[AUTH ERROR] Password auth: User registration failed` - When backend registration fails
- `[AUTH DEBUG] Password auth: Authentication successful` - When authentication succeeds
- `[AUTH WARN] Password auth: Invalid password` - When wrong password is provided

### Server-Side Auth Service (Easy Auth)
- `[AUTH DEBUG] IsAuthenticatedAsync called` - When checking authentication status
- `[AUTH DEBUG] Auth bypassed for development` - When development bypass is used
- `[AUTH DEBUG] GetCurrentUserAsync called` - When getting current user
- `[AUTH DEBUG] GetEasyAuthUserAsync called` - When calling Easy Auth endpoint
- `[AUTH DEBUG] Authentication check starting` - Start of auth check with base URL
- `[AUTH DEBUG] Token refresh needed/not needed` - Token refresh decision with timing info
- `[AUTH DEBUG] Making /.auth/me request` - Before calling /.auth/me endpoint
- `[AUTH DEBUG] /.auth/me response received` - Response status from /.auth/me
- `[AUTH DEBUG] /.auth/me response content` - Content analysis (length, empty checks)
- `[AUTH DEBUG] Parsed Easy Auth user` - Successfully parsed user from response
- `[AUTH DEBUG] Extracted user details` - User details extracted from claims
- `[AUTH DEBUG] Updated last login time and created authenticated user` - Final user creation
- `[AUTH DEBUG] TryRefreshTokenAsync called` - When attempting token refresh
- `[AUTH DEBUG] Sending token refresh request` - Before sending refresh request
- `[AUTH DEBUG] Token refresh response received` - Refresh response status
- `[AUTH DEBUG] Token refresh successful` - When refresh succeeds
- `[AUTH WARN] Token refresh failed` - When refresh fails with status code
- `[AUTH DEBUG] HandlePostLoginRegistrationAsync called` - Post-login registration start
- `[AUTH DEBUG] Post-login registration for user` - User details for registration
- `[AUTH DEBUG] Skipping auto-registration for auth provider` - When skipping registration
- `[AUTH DEBUG] Performing auto-registration` - When performing registration
- `[AUTH DEBUG] Post-login registration completed successfully` - Registration success

### Error Logging
- `[AUTH ERROR] HttpContext not available` - When HTTP context is missing
- `[AUTH ERROR] Network error during authentication check` - HTTP request failures
- `[AUTH ERROR] Failed to parse Easy Auth response` - JSON parsing errors
- `[AUTH ERROR] Unexpected error during authentication check` - Unexpected exceptions
- `[AUTH ERROR] Token refresh exception` - Token refresh failures
- `[AUTH ERROR] Error during post-login registration` - Registration failures

All error logs include detailed information such as error messages, exception types, and stack traces when available.

## Usage

1. **Enable debugging** by setting `Authentication:DEBUG` to `true` in your configuration
2. **Open browser dev tools** (F12) and navigate to the Console tab
3. **Perform authentication actions** (login, logout, page refresh, etc.)
4. **Monitor the console** for authentication debug messages with `[AUTH DEBUG]`, `[AUTH WARN]`, and `[AUTH ERROR]` prefixes

## Log Levels

- **`[AUTH DEBUG]`** - General debugging information (logged via `console.log`)
- **`[AUTH WARN]`** - Warning conditions (logged via `console.warn`)  
- **`[AUTH ERROR]`** - Error conditions (logged via `console.error`)

## Security Note

**Important:** Authentication debugging should only be enabled in development environments. Make sure to set `Authentication:DEBUG` to `false` in production configurations to prevent sensitive authentication information from being logged to browser consoles.

## Implementation

The debugging functionality is implemented in `UserSettingsService` with the following helper methods:
- `LogAuthDebugAsync(string message, object? data = null)`
- `LogAuthErrorAsync(string message, object? error = null)`  
- `LogAuthWarnAsync(string message, object? data = null)`

These methods check the cached `_authDebugEnabled` flag (set during service construction) and only log when debugging is enabled. The debug flag is cached at startup for optimal performance, avoiding repeated configuration lookups.

### Performance Optimization
The `Authentication:DEBUG` configuration value is read once during `UserSettingsService` construction and cached in the `_authDebugEnabled` field. This eliminates the need to query the configuration on every log call, improving performance while ensuring the debug setting remains consistent throughout the application lifecycle.
