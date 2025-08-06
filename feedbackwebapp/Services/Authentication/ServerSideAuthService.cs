using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using SharedDump.Models.Authentication;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using FeedbackWebApp.Services.Account;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Exception thrown when user registration fails
/// </summary>
public class UserRegistrationException : Exception
{
    public bool ShouldLogout { get; }
    
    public UserRegistrationException(string message, bool shouldLogout = false) : base(message)
    {
        ShouldLogout = shouldLogout;
    }
    
    public UserRegistrationException(string message, Exception innerException, bool shouldLogout = false) : base(message, innerException)
    {
        ShouldLogout = shouldLogout;
    }
}

/// <summary>
/// Server-side Azure Easy Auth implementation that uses HttpContext and cookies
/// </summary>
public class ServerSideAuthService : IAuthenticationService, IDisposable
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerSideAuthService> _logger;
    private readonly UserSettingsService _userSettingsService;
    private readonly IRegistrationErrorService? _registrationErrorService;

    // Cache for authenticated user to reduce .auth/me calls
    private AuthenticatedUser? _cachedUser;
    private DateTime? _cacheExpiry;
    private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromMinutes(30); // Cache for 30 minutes
    private readonly object _cacheLock = new object();
    
    // Semaphore to ensure only one authentication check happens at a time
    private readonly SemaphoreSlim _authSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _registrationSemaphore = new SemaphoreSlim(1, 1);

    public event EventHandler<bool>? AuthenticationStateChanged;

    public ServerSideAuthService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<ServerSideAuthService> logger,
        UserSettingsService userSettingsService,
        IRegistrationErrorService? registrationErrorService = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _serviceProvider = serviceProvider;
        _logger = logger;
        _userSettingsService = userSettingsService;
        _registrationErrorService = registrationErrorService;
    }

    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync()
    {
        await _userSettingsService.LogAuthDebugAsync("IsAuthenticatedAsync called");
        
        // Check if auth is bypassed for development
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

        if (bypassAuth && isDevelopment)
        {
            await _userSettingsService.LogAuthDebugAsync("Auth bypassed for development");
            return true;
        }

        try
        {
            // Fast-track check: if user hasn't even attempted to log in, skip expensive OAuth checks
            var hasLoginAttempt = await _userSettingsService.GetLoginAttemptAsync();
            if (!hasLoginAttempt)
            {
                await _userSettingsService.LogAuthDebugAsync("No login attempt detected - user has not tried to log in");
                return false;
            }

            // Try to get user from cache first
            var cachedUser = GetCachedUser();
            if (cachedUser != null)
            {
                await _userSettingsService.LogAuthDebugAsync("IsAuthenticatedAsync result from cache", new { isAuthenticated = true, hasUserInfo = true });
                return true;
            }

            // Check with Easy Auth to see if user has an OAuth session
            var userInfo = await GetEasyAuthUserAsync();
            if (userInfo == null)
            {
                await _userSettingsService.LogAuthDebugAsync("IsAuthenticatedAsync result from Easy Auth", new { isAuthenticated = false, hasUserInfo = false });
                
                // Clear login attempt and last login since OAuth session is gone
                await _userSettingsService.LogAuthDebugAsync("OAuth session expired, clearing login attempt and last login timestamp");
                await _userSettingsService.ClearLoginAttemptAsync();
                await _userSettingsService.RemoveFromLocalStorageAsync("feedbackflow_last_login");
                return false;
            }

            // User is authenticated via OAuth, now check if they're registered in our database
            await _userSettingsService.LogAuthDebugAsync("User authenticated via OAuth, checking database registration", new { 
                provider = userInfo.AuthProvider,
                userId = userInfo.UserId,
                email = userInfo.Email
            });

            try
            {
                // Check if user account exists in database
                await EnsureUserIsRegisteredAsync(userInfo);
                await _userSettingsService.LogAuthDebugAsync("User registration check completed successfully");
                
                return true;
            }
            catch (UserRegistrationException ex)
            {
                await _userSettingsService.LogAuthErrorAsync("User registration check failed", new { 
                    error = ex.Message,
                    shouldLogout = ex.ShouldLogout
                });
                
                // Clear login attempt flag on registration failure
                await _userSettingsService.ClearLoginAttemptAsync();
                
                // Trigger registration error event for UI to handle
                _registrationErrorService?.TriggerRegistrationError(ex.Message);
                
                // If user registration failed and we should log them out, do so
                if (ex.ShouldLogout)
                {
                    _logger.LogWarning("User registration failed, logging out: {Message}", ex.Message);
                    await LogoutAsync();
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error in IsAuthenticatedAsync", new { error = ex.Message, stackTrace = ex.StackTrace });
            _logger.LogWarning(ex, "Error checking authentication status");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticatedUser?> GetCurrentUserAsync()
    {
        await _userSettingsService.LogAuthDebugAsync("GetCurrentUserAsync called");
        
        // Check if auth is bypassed for development
        var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
        var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

        if (bypassAuth && isDevelopment)
        {
            await _userSettingsService.LogAuthDebugAsync("Returning development user", new { bypassAuth, isDevelopment });
            var devUser = CreateDevelopmentUser();
            SetCachedUser(devUser); // Cache the development user too
            return devUser;
        }

        try
        {
            // Try to get user from cache first
            var cachedUser = GetCachedUser();
            if (cachedUser != null)
            {
                await _userSettingsService.LogAuthDebugAsync("GetCurrentUserAsync result from cache", new { 
                    hasUser = true, 
                    userId = cachedUser.UserId, 
                    email = cachedUser.Email, 
                    provider = cachedUser.AuthProvider 
                });
                return cachedUser;
            }

            // If not in cache, get from Easy Auth
            var user = await GetEasyAuthUserAsync();
            if (user != null)
            {
                try
                {
                    // Ensure user is registered in database
                    await EnsureUserIsRegisteredAsync(user);
                    await _userSettingsService.LogAuthDebugAsync("GetCurrentUserAsync: User registration check passed", new { 
                        userId = user.UserId,
                        provider = user.AuthProvider
                    });
                }
                catch (UserRegistrationException ex)
                {
                    await _userSettingsService.LogAuthErrorAsync("GetCurrentUserAsync: User registration check failed", new { 
                        error = ex.Message,
                        shouldLogout = ex.ShouldLogout
                    });
                    
                    // Trigger registration error event for UI to handle
                    _registrationErrorService?.TriggerRegistrationError(ex.Message);
                    
                    // If user registration failed and we should log them out, clear user and return null
                    if (ex.ShouldLogout)
                    {
                        _logger.LogWarning("User registration failed in GetCurrentUserAsync, clearing authentication: {Message}", ex.Message);
                        await LogoutAsync();
                        return null;
                    }
                }
            }
            
            await _userSettingsService.LogAuthDebugAsync("GetCurrentUserAsync result from Easy Auth", new { 
                hasUser = user != null, 
                userId = user?.UserId, 
                email = user?.Email, 
                provider = user?.AuthProvider 
            });
            return user;
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error in GetCurrentUserAsync", new { error = ex.Message, stackTrace = ex.StackTrace });
            _logger.LogWarning(ex, "Error getting current user");
            return null;
        }
    }

    /// <inheritdoc />
    public string GetLoginUrl(string provider, string? redirectUrl = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext not available");

        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var providerPath = provider.ToLower() switch
        {
            "microsoft" => "aad",
            "google" => "google",
            "github" => "github",
            "facebook" => "facebook",
            "twitter" => "twitter",
            _ => provider.ToLower()
        };

        var loginUrl = $"{baseUrl}/.auth/login/{providerPath}";
        
        // Build query parameters
        var queryParams = new List<string>();
        
        // Add redirect URL parameter
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            var encodedRedirect = Uri.EscapeDataString(redirectUrl);
            queryParams.Add($"post_login_redirect_url={encodedRedirect}");
        }
        else
        {
            // Default redirect to home page
            var defaultRedirect = Uri.EscapeDataString($"{baseUrl}/");
            queryParams.Add($"post_login_redirect_url={defaultRedirect}");
        }
        
        // Add Google-specific parameter for refresh tokens
        if (provider.ToLower() == "google")
        {
            queryParams.Add("access_type=offline");
        }
        
        // Add GitHub-specific scopes for email access
        if (provider.ToLower() == "github")
        {
            queryParams.Add("scope=user:email");
        }
        
        // Append query parameters
        if (queryParams.Count > 0)
        {
            loginUrl += "?" + string.Join("&", queryParams);
        }

        return loginUrl;
    }

    /// <inheritdoc />
    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext not available");

        // Clear the cached user
        ClearUserCache();

        // Clear the last login timestamp and login attempt flag to ensure fast-track authentication check works
        await _userSettingsService.RemoveFromLocalStorageAsync("feedbackflow_last_login");
        await _userSettingsService.ClearLoginAttemptAsync();

        // Trigger authentication state change
        AuthenticationStateChanged?.Invoke(this, false);
        
        // Redirect to logout endpoint with post-logout redirect to home page
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var homeRedirect = Uri.EscapeDataString($"{baseUrl}/");
        var logoutUrl = $"{baseUrl}/.auth/logout?post_logout_redirect_uri={homeRedirect}";
        
        httpContext.Response.Redirect(logoutUrl);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get user information from Azure Easy Auth using server-side HttpClient with forwarded cookies
    /// </summary>
    /// <returns>Authenticated user or null</returns>
    private async Task<AuthenticatedUser?> GetEasyAuthUserAsync()
    {
        await _userSettingsService.LogAuthDebugAsync("GetEasyAuthUserAsync called");
        
        // Use semaphore to prevent concurrent authentication calls
        await _authSemaphore.WaitAsync();
        try
        {
            // Double-check cache after acquiring semaphore (in case another thread cached while we were waiting)
            var cachedUserAfterWait = GetCachedUser();
            if (cachedUserAfterWait != null)
            {
                await _userSettingsService.LogAuthDebugAsync("GetEasyAuthUserAsync result from cache after semaphore wait", new { 
                    hasUser = true, 
                    userId = cachedUserAfterWait.UserId, 
                    email = cachedUserAfterWait.Email, 
                    provider = cachedUserAfterWait.AuthProvider 
                });
                return cachedUserAfterWait;
            }
            
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                await _userSettingsService.LogAuthErrorAsync("HttpContext not available for authentication check");
                _logger.LogWarning("HttpContext not available for authentication check");
                return null;
            }

            try
            {
                var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                await _userSettingsService.LogAuthDebugAsync("Authentication check starting", new { baseUrl });
                
                // Check if we should refresh the token (if it's been over 2 hours since last login)
                var lastLogin = await _userSettingsService.GetLastLoginAtAsync();
                if (lastLogin.HasValue && DateTime.UtcNow.Subtract(lastLogin.Value).TotalHours > 2)
                {
                    await _userSettingsService.LogAuthDebugAsync("Token refresh needed", new { 
                        lastLogin, 
                        hoursSinceLogin = DateTime.UtcNow.Subtract(lastLogin.Value).TotalHours 
                    });
                    _logger.LogDebug("Last login was over 2 hours ago, attempting token refresh");
                    await TryRefreshTokenAsync(baseUrl, httpContext);
                }
                else
                {
                    await _userSettingsService.LogAuthDebugAsync("Token refresh not needed", new { 
                        lastLogin, 
                        hoursSinceLogin = lastLogin.HasValue ? DateTime.UtcNow.Subtract(lastLogin.Value).TotalHours : (double?)null
                    });
                }
                
                // Create request to /.auth/me with forwarded cookies
                var authMeUrl = $"{baseUrl}/.auth/me";
                HttpResponseMessage response;
                bool retryAttempted = false;
                
                // First attempt
                var request = new HttpRequestMessage(HttpMethod.Get, authMeUrl);
                
                // Forward all cookies from the current request
                var hasCookies = httpContext.Request.Headers.ContainsKey("Cookie");
                if (hasCookies)
                {
                    request.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
                }
                await _userSettingsService.LogAuthDebugAsync("Making /.auth/me request (first attempt)", new { authMeUrl, hasCookies });

                // Add cache control to ensure fresh data
                request.Headers.Add("Cache-Control", "no-cache");

                response = await _httpClient.SendAsync(request);
                await _userSettingsService.LogAuthDebugAsync("/.auth/me response received (first attempt)", new { 
                    statusCode = response.StatusCode, 
                    isSuccess = response.IsSuccessStatusCode 
                });
                
                // If first attempt fails, try refreshing token and retry once
                if (!response.IsSuccessStatusCode && !retryAttempted)
                {
                    await _userSettingsService.LogAuthDebugAsync("First /.auth/me attempt failed, trying token refresh", new { statusCode = response.StatusCode });
                    _logger.LogDebug("First authentication check failed with status: {StatusCode}, attempting token refresh", response.StatusCode);
                    
                    // Attempt token refresh
                    await TryRefreshTokenAsync(baseUrl, httpContext);
                    retryAttempted = true;
                    
                    // Dispose the failed response
                    response.Dispose();
                    
                    // Retry the request with fresh cookies
                    var retryRequest = new HttpRequestMessage(HttpMethod.Get, authMeUrl);
                    
                    // Forward cookies again (they may have been updated by refresh)
                    if (httpContext.Request.Headers.ContainsKey("Cookie"))
                    {
                        retryRequest.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
                    }
                    retryRequest.Headers.Add("Cache-Control", "no-cache");
                    
                    await _userSettingsService.LogAuthDebugAsync("Making /.auth/me request (retry after refresh)", new { authMeUrl });
                    response = await _httpClient.SendAsync(retryRequest);
                    
                    await _userSettingsService.LogAuthDebugAsync("/.auth/me response received (retry attempt)", new { 
                        statusCode = response.StatusCode, 
                        isSuccess = response.IsSuccessStatusCode 
                    });
                }
                
                // If still failing after retry, return null
                if (!response.IsSuccessStatusCode)
                {
                    var attemptType = retryAttempted ? "after token refresh retry" : "on first attempt";
                    await _userSettingsService.LogAuthWarnAsync($"Authentication check failed {attemptType}", new { statusCode = response.StatusCode, retryAttempted });
                    _logger.LogDebug("Authentication check failed {AttemptType} with status: {StatusCode}", attemptType, response.StatusCode);
                    
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                await _userSettingsService.LogAuthDebugAsync("/.auth/me response content", new { 
                    contentLength = responseContent?.Length ?? 0,
                    isEmpty = string.IsNullOrEmpty(responseContent),
                    isEmptyArray = responseContent?.Trim() == "[]"
                });
                
                if (string.IsNullOrEmpty(responseContent) || responseContent.Trim() == "[]")
                {
                    await _userSettingsService.LogAuthDebugAsync("No authenticated user found in response");
                    _logger.LogDebug("No authenticated user found");
                    return null;
                }

                // Parse the Easy Auth response
                var userArray = JsonSerializer.Deserialize<EasyAuthUser[]>(responseContent);
                if (userArray == null || userArray.Length == 0)
                {
                    await _userSettingsService.LogAuthWarnAsync("Failed to parse user array or empty array", new { 
                        isNull = userArray == null, 
                        length = userArray?.Length ?? 0 
                    });
                    _logger.LogDebug("Empty user array from Easy Auth");
                    return null;
                }

                var userInfo = userArray[0];
                var provider = GetProviderFromIdentityProvider(userInfo.ProviderName);
                await _userSettingsService.LogAuthDebugAsync("Parsed Easy Auth user", new { 
                    userId = userInfo.UserId,
                    providerName = userInfo.ProviderName,
                    mappedProvider = provider,
                    claimsCount = userInfo.UserClaims?.Length ?? 0
                });
                
                // Extract user details from claims
                var email = GetEmailFromClaims(userInfo.UserClaims, provider);
                
                // For GitHub users without email, we'll try to get it from API during registration process
                // For now, continue with what we have from claims
                if (string.IsNullOrEmpty(email) && provider == "GitHub")
                {
                    await _userSettingsService.LogAuthDebugAsync("GitHub user has no email in claims, will attempt API fetch during registration if needed");
                }
                
                var name = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "name")?.Val ??
                        userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Val ??
                        userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Val ??
                        userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "given_name")?.Val ??
                        userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "preferred_username")?.Val;
                
                // Provider-specific fallbacks for name
                if (string.IsNullOrEmpty(name))
                {
                    name = provider switch
                    {
                        "GitHub" => userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "urn:github:login")?.Val,
                        _ => null
                    };
                }
                        
                // Final fallbacks for name
                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                    {
                        name = email.Split('@')[0];
                    }
                    else
                    {
                        name = userInfo.UserId ?? "User";
                    }
                }
                
                var profileImageUrl = GetProfileImageUrl(userInfo.ProviderName, userInfo.UserClaims);
                
                await _userSettingsService.LogAuthDebugAsync("Extracted user details", new { 
                    email, 
                    name, 
                    profileImageUrl, 
                    provider 
                });
                        
                var authenticatedUser = new AuthenticatedUser
                {
                    UserId = userInfo.UserId ?? string.Empty,
                    Email = email ?? string.Empty,
                    Name = name,
                    AuthProvider = provider,
                    ProviderUserId = userInfo.UserId ?? string.Empty,
                    ProfileImageUrl = profileImageUrl,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };
                
                // Update last login time in user settings
                await _userSettingsService.UpdateLastLoginAtAsync();
                await _userSettingsService.LogAuthDebugAsync("Updated last login time and created authenticated user", new { 
                    userId = authenticatedUser.UserId,
                    email = authenticatedUser.Email,
                    provider = authenticatedUser.AuthProvider
                });
                
                // Cache the authenticated user
                SetCachedUser(authenticatedUser);
                
                _logger.LogDebug("Successfully authenticated user: {Email}", email);
                return authenticatedUser;
            }
            catch (HttpRequestException ex)
            {
                await _userSettingsService.LogAuthErrorAsync("Network error during authentication check", new { 
                    error = ex.Message, 
                    innerException = ex.InnerException?.Message 
                });
                _logger.LogWarning(ex, "Network error during authentication check");
                return null;
            }
            catch (JsonException ex)
            {
                await _userSettingsService.LogAuthErrorAsync("Failed to parse Easy Auth response", new { 
                    error = ex.Message, 
                    path = ex.Path 
                });
                _logger.LogError(ex, "Failed to parse Easy Auth response");
                return null;
            }
            catch (Exception ex)
            {
                await _userSettingsService.LogAuthErrorAsync("Unexpected error during authentication check", new { 
                    error = ex.Message, 
                    type = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                });
                _logger.LogError(ex, "Unexpected error during authentication check");
                return null;
            }
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    /// <summary>
    /// Attempt to refresh the authentication token via /.auth/refresh
    /// </summary>
    /// <param name="baseUrl">Base URL of the application</param>
    /// <param name="httpContext">Current HTTP context</param>
    private async Task TryRefreshTokenAsync(string baseUrl, HttpContext httpContext)
    {
        await _userSettingsService.LogAuthDebugAsync("TryRefreshTokenAsync called", new { baseUrl });
        
        try
        {
            var refreshUrl = $"{baseUrl}/.auth/refresh";
            var refreshRequest = new HttpRequestMessage(HttpMethod.Get, refreshUrl);
            
            // Forward all cookies from the current request
            var hasCookies = httpContext.Request.Headers.ContainsKey("Cookie");
            if (hasCookies)
            {
                refreshRequest.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
            }

            // Add cache control to ensure fresh data
            refreshRequest.Headers.Add("Cache-Control", "no-cache");

            await _userSettingsService.LogAuthDebugAsync("Sending token refresh request", new { refreshUrl, hasCookies });
            _logger.LogDebug("Attempting to refresh authentication token");
            
            var refreshResponse = await _httpClient.SendAsync(refreshRequest);
            
            await _userSettingsService.LogAuthDebugAsync("Token refresh response received", new { 
                statusCode = refreshResponse.StatusCode,
                isSuccess = refreshResponse.IsSuccessStatusCode
            });

            if (refreshResponse.IsSuccessStatusCode)
            {
                await _userSettingsService.LogAuthDebugAsync("Token refresh successful");
                _logger.LogDebug("Token refresh successful");
                
                // Apply any Set-Cookie headers from the refresh response
                await ApplySetCookieHeadersAsync(refreshResponse, httpContext);
                
                // Clear cached user since token was refreshed
                ClearUserCache();
                
                // Update last login time in user settings
                await _userSettingsService.UpdateLastLoginAtAsync();
            }
            else
            {
                await _userSettingsService.LogAuthWarnAsync("Token refresh failed", new { statusCode = refreshResponse.StatusCode });
                _logger.LogDebug("Token refresh failed with status: {StatusCode}", refreshResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Token refresh exception", new { 
                error = ex.Message, 
                type = ex.GetType().Name 
            });
            _logger.LogWarning(ex, "Token refresh attempt failed, but continuing with authentication check");
        }
    }

    /// <summary>
    /// Apply Set-Cookie headers from a response to the current HttpContext
    /// </summary>
    /// <param name="response">HTTP response containing Set-Cookie headers</param>
    /// <param name="httpContext">Current HTTP context to apply cookies to</param>
    private async Task ApplySetCookieHeadersAsync(HttpResponseMessage response, HttpContext httpContext)
    {
        try
        {
            // Get Set-Cookie headers from the response
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            {
                var appliedCookiesCount = 0;
                
                foreach (var setCookieHeader in setCookieHeaders)
                {
                    // Parse the Set-Cookie header
                    var cookieParts = setCookieHeader.Split(';');
                    if (cookieParts.Length > 0)
                    {
                        var nameValuePart = cookieParts[0].Trim();
                        var equalIndex = nameValuePart.IndexOf('=');
                        
                        if (equalIndex > 0)
                        {
                            var cookieName = nameValuePart.Substring(0, equalIndex).Trim();
                            var cookieValue = nameValuePart.Substring(equalIndex + 1).Trim();
                            
                            // Create cookie options by parsing other parts
                            var cookieOptions = new CookieOptions();
                            
                            foreach (var part in cookieParts.Skip(1))
                            {
                                var trimmedPart = part.Trim();
                                if (trimmedPart.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                                {
                                    cookieOptions.Path = trimmedPart.Substring(5);
                                }
                                else if (trimmedPart.StartsWith("Domain=", StringComparison.OrdinalIgnoreCase))
                                {
                                    cookieOptions.Domain = trimmedPart.Substring(7);
                                }
                                else if (trimmedPart.StartsWith("Max-Age=", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (int.TryParse(trimmedPart.Substring(8), out var maxAge))
                                    {
                                        cookieOptions.MaxAge = TimeSpan.FromSeconds(maxAge);
                                    }
                                }
                                else if (trimmedPart.StartsWith("Expires=", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (DateTime.TryParse(trimmedPart.Substring(8), out var expires))
                                    {
                                        cookieOptions.Expires = expires;
                                    }
                                }
                                else if (string.Equals(trimmedPart, "HttpOnly", StringComparison.OrdinalIgnoreCase))
                                {
                                    cookieOptions.HttpOnly = true;
                                }
                                else if (string.Equals(trimmedPart, "Secure", StringComparison.OrdinalIgnoreCase))
                                {
                                    cookieOptions.Secure = true;
                                }
                                else if (trimmedPart.StartsWith("SameSite=", StringComparison.OrdinalIgnoreCase))
                                {
                                    var sameSiteValue = trimmedPart.Substring(9);
                                    if (Enum.TryParse<SameSiteMode>(sameSiteValue, true, out var sameSite))
                                    {
                                        cookieOptions.SameSite = sameSite;
                                    }
                                }
                            }
                            
                            // Apply the cookie to the response
                            httpContext.Response.Cookies.Append(cookieName, cookieValue, cookieOptions);
                            appliedCookiesCount++;
                            
                            await _userSettingsService.LogAuthDebugAsync("Applied Set-Cookie header", new { 
                                cookieName, 
                                cookieValueLength = cookieValue?.Length ?? 0,
                                path = cookieOptions.Path,
                                domain = cookieOptions.Domain,
                                httpOnly = cookieOptions.HttpOnly,
                                secure = cookieOptions.Secure,
                                sameSite = cookieOptions.SameSite.ToString()
                            });
                        }
                    }
                }
                
                await _userSettingsService.LogAuthDebugAsync("Applied Set-Cookie headers from refresh response", new { 
                    totalHeaders = setCookieHeaders.Count(), 
                    appliedCookies = appliedCookiesCount 
                });
                
                _logger.LogDebug("Applied {AppliedCount} cookies from {TotalCount} Set-Cookie headers", 
                    appliedCookiesCount, setCookieHeaders.Count());
            }
            else
            {
                await _userSettingsService.LogAuthDebugAsync("No Set-Cookie headers found in refresh response");
                _logger.LogDebug("No Set-Cookie headers found in refresh response");
            }
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error applying Set-Cookie headers from refresh response", new { 
                error = ex.Message, 
                type = ex.GetType().Name 
            });
            _logger.LogWarning(ex, "Error applying Set-Cookie headers from refresh response");
        }
    }

    /// <summary>
    /// Ensure the user is registered in our database. If not, attempt auto-registration.
    /// </summary>
    /// <param name="user">Authenticated user information</param>
    /// <exception cref="UserRegistrationException">Thrown when registration fails</exception>
    private async Task EnsureUserIsRegisteredAsync(AuthenticatedUser user)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountServiceProvider = scope.ServiceProvider.GetService<IAccountServiceProvider>();
            
            if (accountServiceProvider != null)
            {
                var webAppAccountService = accountServiceProvider.GetService();
                
                // Try to get user account to see if they're registered
                var accountResult = await webAppAccountService.GetUserAccountAndLimitsAsync();
                
                if (accountResult == null)
                {
                    await _userSettingsService.LogAuthDebugAsync("User account not found, attempting auto-registration", new { 
                        userId = user.UserId,
                        provider = user.AuthProvider
                    });
                    
                    // For GitHub users without email, try to get it from API before registration
                    var enhancedEmail = user.Email;
                    if (user.AuthProvider == "GitHub" && string.IsNullOrEmpty(user.Email))
                    {
                        await TryEnhanceGitHubUserEmailAsync(user);
                        enhancedEmail = user.Email; // Use the enhanced email from GitHub API
                    }
                    
                    // User doesn't exist, attempt auto-registration
                    var userManagementService = scope.ServiceProvider.GetService<IUserManagementService>();
                    if (userManagementService != null)
                    {
                        var registrationResult = await userManagementService.RegisterCurrentUserAsync(enhancedEmail);
                        
                        if (!registrationResult.Success)
                        {
                            var errorMessage = $"Failed to register user: {registrationResult.ErrorMessage}";
                            await _userSettingsService.LogAuthErrorAsync("Auto-registration failed", new { 
                                error = registrationResult.ErrorMessage,
                                statusCode = registrationResult.StatusCode,
                                enhancedEmail = enhancedEmail
                            });
                            
                            throw new UserRegistrationException(errorMessage, shouldLogout: true);
                        }
                        
                        await _userSettingsService.LogAuthDebugAsync("Auto-registration completed successfully", new { 
                            userId = user.UserId,
                            enhancedEmail = enhancedEmail
                        });
                        _logger.LogInformation("Successfully auto-registered user {UserId} from {Provider} provider", 
                            user.UserId, user.AuthProvider);
                        
                        // Set last login timestamp after successful registration
                        await _userSettingsService.UpdateLastLoginAtAsync();
                    }
                    else
                    {
                        throw new UserRegistrationException("User management service not available", shouldLogout: true);
                    }
                }
                else
                {
                    await _userSettingsService.LogAuthDebugAsync("User account found in database", new { 
                        userId = user.UserId,
                        tier = accountResult.Value.account?.Tier.ToString()
                    });
                    
                    // Set last login timestamp for existing user
                    await _userSettingsService.UpdateLastLoginAtAsync();
                }
            }
            else
            {
                throw new UserRegistrationException("Account service not available", shouldLogout: true);
            }
        }
        catch (UserRegistrationException)
        {
            // Re-throw user registration exceptions without wrapping
            throw;
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Unexpected error during user registration check", new { 
                error = ex.Message, 
                type = ex.GetType().Name
            });
            _logger.LogError(ex, "Unexpected error during user registration check for user {UserId}", user.UserId);
            throw new UserRegistrationException($"Registration check failed: {ex.Message}", shouldLogout: true);
        }
    }

    /// <summary>
    /// Auto-register the current user in the backend system
    /// </summary>
    private async Task AutoRegisterUserAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userManagementService = scope.ServiceProvider.GetService<IUserManagementService>();
            
            if (userManagementService != null)
            {
                await userManagementService.RegisterCurrentUserAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-register user");
        }
    }

    /// <summary>
    /// Handle post-login user registration for OAuth providers (GitHub, Google, etc.)
    /// This should only be called after a user completes an OAuth login flow
    /// </summary>
    /// <returns>True if registration was successful, false otherwise</returns>
    public async Task<bool> HandlePostLoginRegistrationAsync()
    {
        await _userSettingsService.LogAuthDebugAsync("HandlePostLoginRegistrationAsync called");
        
        // Use semaphore to prevent double registration
        await _registrationSemaphore.WaitAsync();
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                await _userSettingsService.LogAuthErrorAsync("HttpContext not available for post-login registration");
                _logger.LogWarning("HttpContext not available for post-login registration");
                return false;
            }

            // Check if user is authenticated via OAuth providers (not password auth)
            var user = await GetEasyAuthUserAsync();
            if (user == null)
            {
                await _userSettingsService.LogAuthDebugAsync("No authenticated user found for post-login registration");
                _logger.LogDebug("No authenticated user found for post-login registration");
                return false;
            }

            await _userSettingsService.LogAuthDebugAsync("Post-login registration for user", new { 
                provider = user.AuthProvider,
                userId = user.UserId,
                email = user.Email
            });

            // Update last login time in user settings
            await _userSettingsService.UpdateLastLoginAtAsync();

            // Only auto-register for OAuth providers (GitHub, Google, Microsoft), not for password auth
            if (user.AuthProvider == "Password" || user.AuthProvider == "Development")
            {
                await _userSettingsService.LogAuthDebugAsync("Skipping auto-registration for auth provider", new { provider = user.AuthProvider });
                _logger.LogDebug("Skipping auto-registration for {AuthProvider} provider", user.AuthProvider);
                return true;
            }

            await _userSettingsService.LogAuthDebugAsync("Performing auto-registration", new { provider = user.AuthProvider });
            _logger.LogInformation("Performing post-login registration for user from {AuthProvider} provider", user.AuthProvider);

            // Perform registration and await the result
            await AutoRegisterUserAsync();
            
            // Clear cache after registration in case user information changed
            ClearUserCache();
            
            await _userSettingsService.LogAuthDebugAsync("Post-login registration completed successfully", new { provider = user.AuthProvider });
            _logger.LogInformation("Post-login registration completed successfully for {AuthProvider} user", user.AuthProvider);
            return true;
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error during post-login registration", new { 
                error = ex.Message, 
                type = ex.GetType().Name,
                stackTrace = ex.StackTrace
            });
            _logger.LogError(ex, "Error during post-login registration");
            return false;
        }
        finally
        {
            _registrationSemaphore.Release();
        }
    }

    /// <summary>
    /// Get cached user if still valid
    /// </summary>
    /// <returns>Cached user or null if cache is invalid/expired</returns>
    private AuthenticatedUser? GetCachedUser()
    {
        lock (_cacheLock)
        {
            if (_cachedUser != null && _cacheExpiry.HasValue && DateTime.UtcNow < _cacheExpiry.Value)
            {
                return _cachedUser;
            }
            
            // Cache expired or invalid, clear it
            if (_cachedUser != null)
            {
                _cachedUser = null;
                _cacheExpiry = null;
            }
            
            return null;
        }
    }

    /// <summary>
    /// Cache the authenticated user
    /// </summary>
    /// <param name="user">User to cache</param>
    private void SetCachedUser(AuthenticatedUser user)
    {
        lock (_cacheLock)
        {
            _cachedUser = user;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheValidityPeriod);
        }
    }

    /// <summary>
    /// Clear the user cache
    /// </summary>
    private void ClearUserCache()
    {
        lock (_cacheLock)
        {
            _cachedUser = null;
            _cacheExpiry = null;
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
    /// Extract email from claims based on provider-specific logic
    /// </summary>
    /// <param name="claims">User claims from Easy Auth</param>
    /// <param name="provider">Authentication provider</param>
    /// <returns>User email or null if not found</returns>
    private static string? GetEmailFromClaims(EasyAuthClaim[]? claims, string provider)
    {
        if (claims == null) return null;

        // Standard email claim types to check, in order of preference
        var emailClaimTypes = new[]
        {
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            "email",
            "emails",
            "preferred_username"
        };

        // Provider-specific email claim types
        var providerSpecificClaims = provider switch
        {
            "GitHub" => new[] { "urn:github:email", "urn:github:primary_email" },
            "Google" => new[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn" },
            "Microsoft" => new[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "upn" },
            _ => Array.Empty<string>()
        };

        // First, try provider-specific claims
        foreach (var claimType in providerSpecificClaims)
        {
            var claim = claims.FirstOrDefault(c => string.Equals(c.Typ, claimType, StringComparison.OrdinalIgnoreCase));
            if (claim?.Val != null && IsValidEmail(claim.Val))
            {
                return claim.Val;
            }
        }

        // Then, try standard email claim types
        foreach (var claimType in emailClaimTypes)
        {
            var claim = claims.FirstOrDefault(c => string.Equals(c.Typ, claimType, StringComparison.OrdinalIgnoreCase));
            if (claim?.Val != null && IsValidEmail(claim.Val))
            {
                return claim.Val;
            }
        }

        return null;
    }

    /// <summary>
    /// Try to enhance GitHub user with email from API during registration
    /// </summary>
    /// <param name="user">User to enhance</param>
    private async Task TryEnhanceGitHubUserEmailAsync(AuthenticatedUser user)
    {
        try
        {
            await _userSettingsService.LogAuthDebugAsync("Attempting to enhance GitHub user email during registration", new { 
                userId = user.UserId,
                hasEmail = !string.IsNullOrEmpty(user.Email)
            });

            // We need to get the access token from the current Easy Auth session
            var easyAuthUser = await GetCurrentEasyAuthUserInfoAsync();
            if (easyAuthUser?.AccessToken == null)
            {
                await _userSettingsService.LogAuthDebugAsync("No access token available for GitHub email enhancement");
                return;
            }

            var githubEmail = await GetEmailFromGitHubApiAsync(easyAuthUser.AccessToken);
            if (!string.IsNullOrEmpty(githubEmail))
            {
                user.Email = githubEmail;
                await _userSettingsService.LogAuthDebugAsync("Successfully enhanced GitHub user with email from API", new { 
                    email = githubEmail 
                });
                _logger.LogInformation("Enhanced GitHub user {UserId} with email {Email} from API during registration", 
                    user.UserId, githubEmail);
            }
            else
            {
                await _userSettingsService.LogAuthDebugAsync("Could not retrieve email from GitHub API during registration");
            }
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error enhancing GitHub user email during registration", new { 
                error = ex.Message,
                userId = user.UserId
            });
            _logger.LogWarning(ex, "Failed to enhance GitHub user email during registration for user {UserId}", user.UserId);
        }
    }

    /// <summary>
    /// Get current Easy Auth user info with access token
    /// </summary>
    private async Task<EasyAuthUser?> GetCurrentEasyAuthUserInfoAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var authMeUrl = $"{baseUrl}/.auth/me";

            var request = new HttpRequestMessage(HttpMethod.Get, authMeUrl);
            
            // Forward cookies
            if (httpContext.Request.Headers.ContainsKey("Cookie"))
            {
                request.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseContent) || responseContent.Trim() == "[]") return null;

            var userArray = JsonSerializer.Deserialize<EasyAuthUser[]>(responseContent);
            return userArray?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current Easy Auth user info");
            return null;
        }
    }

    /// <summary>
    /// Fetch user email from GitHub API using access token as fallback
    /// </summary>
    /// <param name="accessToken">GitHub access token from Easy Auth</param>
    /// <returns>Primary email address or null if not accessible</returns>
    private async Task<string?> GetEmailFromGitHubApiAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("FeedbackFlow", "1.0"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch GitHub user emails via API. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var emailsJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(emailsJson);
            
            // Find the primary email
            foreach (var emailElement in document.RootElement.EnumerateArray())
            {
                if (emailElement.TryGetProperty("primary", out var primaryProperty) && 
                    primaryProperty.GetBoolean() &&
                    emailElement.TryGetProperty("email", out var emailProperty))
                {
                    var email = emailProperty.GetString();
                    if (!string.IsNullOrEmpty(email) && IsValidEmail(email))
                    {
                        _logger.LogDebug("Successfully fetched primary email from GitHub API");
                        return email;
                    }
                }
            }

            // If no primary email found, try to get the first verified email
            foreach (var emailElement in document.RootElement.EnumerateArray())
            {
                if (emailElement.TryGetProperty("verified", out var verifiedProperty) && 
                    verifiedProperty.GetBoolean() &&
                    emailElement.TryGetProperty("email", out var emailProperty))
                {
                    var email = emailProperty.GetString();
                    if (!string.IsNullOrEmpty(email) && IsValidEmail(email))
                    {
                        _logger.LogDebug("Using first verified email from GitHub API as fallback");
                        return email;
                    }
                }
            }

            _logger.LogDebug("No suitable email found in GitHub API response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching email from GitHub API");
            return null;
        }
    }

    /// <summary>
    /// Simple email validation
    /// </summary>
    /// <param name="email">Email to validate</param>
    /// <returns>True if email format is valid</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Map identity provider names to standard provider names
    /// </summary>
    /// <param name="providerName">Provider name from Azure Easy Auth</param>
    /// <returns>Standardized provider name</returns>
    private static string GetProviderFromIdentityProvider(string providerName)
    {
        return providerName switch
        {
            "aad" => "Microsoft",
            "google" => "Google",
            "github" => "GitHub",
            "facebook" => "Facebook",
            "twitter" => "Twitter",
            _ => providerName
        };
    }

    /// <summary>
    /// Extract profile image URL from provider claims
    /// </summary>
    /// <param name="providerName">Provider name from Azure Easy Auth</param>
    /// <param name="claims">User claims</param>
    /// <returns>Profile image URL or null</returns>
    private static string? GetProfileImageUrl(string providerName, EasyAuthClaim[]? claims)
    {
        if (claims == null) return null;

        return providerName switch
        {
            "aad" => claims.FirstOrDefault(c => c.Typ == "picture")?.Val,
            "google" => claims.FirstOrDefault(c => c.Typ == "picture")?.Val,
            "github" => claims.FirstOrDefault(c => c.Typ == "urn:github:avatar_url")?.Val,
            "facebook" => claims.FirstOrDefault(c => c.Typ == "urn:facebook:picture")?.Val,
            "twitter" => claims.FirstOrDefault(c => c.Typ == "urn:twitter:profile_image_url_https")?.Val,
            _ => null
        };
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        _authSemaphore?.Dispose();
        _registrationSemaphore?.Dispose();
    }
}

/// <summary>
/// Represents the Easy Auth user information from /.auth/me endpoint
/// </summary>
internal class EasyAuthUser
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
    
    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;
    
    [JsonPropertyName("user_claims")]
    public EasyAuthClaim[]? UserClaims { get; set; }
    
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("expires_on")]
    public string? ExpiresOn { get; set; }
    
    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }
}

/// <summary>
/// Represents a claim from Easy Auth
/// </summary>
internal class EasyAuthClaim
{
    [JsonPropertyName("typ")]
    public string Typ { get; set; } = string.Empty;
    
    [JsonPropertyName("val")]
    public string? Val { get; set; }
}
