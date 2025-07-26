using Microsoft.Extensions.Configuration;
using SharedDump.Models.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeedbackWebApp.Services.Authentication;

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

    // Cache for authenticated user to reduce .auth/me calls
    private AuthenticatedUser? _cachedUser;
    private DateTime? _cacheExpiry;
    private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromMinutes(30); // Cache for 30 minutes
    private readonly object _cacheLock = new object();
    
    // Semaphore to ensure only one authentication check happens at a time
    private readonly SemaphoreSlim _authSemaphore = new SemaphoreSlim(1, 1);

    public event EventHandler<bool>? AuthenticationStateChanged;

    public ServerSideAuthService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<ServerSideAuthService> logger,
        UserSettingsService userSettingsService)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _serviceProvider = serviceProvider;
        _logger = logger;
        _userSettingsService = userSettingsService;
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
            // Try to get user from cache first
            var cachedUser = GetCachedUser();
            if (cachedUser != null)
            {
                await _userSettingsService.LogAuthDebugAsync("IsAuthenticatedAsync result from cache", new { isAuthenticated = true, hasUserInfo = true });
                return true;
            }

            // If not in cache, check with Easy Auth
            var userInfo = await GetEasyAuthUserAsync();
            var isAuthenticated = userInfo != null;
            await _userSettingsService.LogAuthDebugAsync("IsAuthenticatedAsync result from Easy Auth", new { isAuthenticated, hasUserInfo = userInfo != null });
            return isAuthenticated;
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

            // If not in cache, get from Easy Auth and cache the result
            var user = await GetEasyAuthUserAsync();
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

    /// <summary>
    /// Get the current access token from the request headers (like JavaScript implementation)
    /// </summary>
    /// <returns>Access token or null if not available</returns>
    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                await _userSettingsService.LogAuthDebugAsync("No HttpContext available for access token retrieval");
                return null;
            }

            var accessToken = GetProviderAccessToken(httpContext);
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                // Check if token is expired
                if (await IsTokenExpiredAsync(accessToken))
                {
                    await _userSettingsService.LogAuthDebugAsync("Access token expired, attempting refresh");
                    var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                    await TryRefreshTokenAsync(baseUrl, httpContext);
                    
                    // Get token again after refresh
                    accessToken = GetProviderAccessToken(httpContext);
                }
            }

            var provider = GetCurrentProvider(httpContext);
            await _userSettingsService.LogAuthDebugAsync("GetAccessTokenAsync result", new { 
                hasToken = !string.IsNullOrEmpty(accessToken),
                provider
            });
            
            return accessToken;
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error getting access token", new { 
                error = ex.Message,
                type = ex.GetType().Name
            });
            _logger.LogWarning(ex, "Error getting access token");
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
        
        // Add provider-specific parameters (based on Microsoft documentation)
        switch (provider.ToLower())
        {
            case "google":
                // Add Google-specific parameter for refresh tokens
                // Per Microsoft docs: "Append an access_type=offline query string parameter to your /.auth/login/google API call"
                queryParams.Add("access_type=offline");
                queryParams.Add("prompt=consent"); // Force consent to get refresh token
                break;
            case "microsoft":
            case "aad":
                // Add scope parameter for offline access (refresh tokens)
                // Per Microsoft docs: loginParameters: ["scope=openid profile email offline_access"]
                queryParams.Add("scope=openid%20profile%20email%20offline_access");
                break;
            case "github":
                // GitHub doesn't explicitly mention refresh tokens in the docs, but we can add scope for better permissions
                queryParams.Add("scope=user:email");
                break;
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

            // First try to get access token from injected headers (more efficient than /.auth/me)
            var accessToken = GetProviderAccessToken(httpContext);
            if (!string.IsNullOrEmpty(accessToken))
            {
                await _userSettingsService.LogAuthDebugAsync("Found access token in headers, checking if token refresh needed");
                
                // Check if token is expired and needs refresh
                if (await IsTokenExpiredAsync(accessToken))
                {
                    await _userSettingsService.LogAuthDebugAsync("Access token is expired, attempting refresh");
                    var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                    await TryRefreshTokenAsync(baseUrl, httpContext);
                    
                    // Re-get the token after refresh attempt
                    accessToken = GetProviderAccessToken(httpContext);
                }
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
            var email = userInfo.UserClaims?.FirstOrDefault(c => c.Typ == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Val;
            
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
    /// Get the access token from provider-specific headers based on Microsoft documentation
    /// </summary>
    /// <param name="httpContext">Current HTTP context</param>
    /// <returns>Access token or null if not found</returns>
    private string? GetProviderAccessToken(HttpContext httpContext)
    {
        // Try different provider-specific token headers based on Microsoft documentation
        // https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-oauth-tokens#retrieve-tokens-in-app-code
        
        var accessTokenHeaders = new[]
        {
            "x-ms-token-aad-access-token",     // Microsoft Entra ID
            "x-ms-token-google-access-token",  // Google
            "x-ms-token-github-access-token",  // GitHub
            "x-ms-token-facebook-access-token", // Facebook
            "x-ms-token-twitter-access-token"   // Twitter/X
        };

        foreach (var header in accessTokenHeaders)
        {
            var token = httpContext.Request.Headers[header].FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the refresh token from provider-specific headers
    /// </summary>
    /// <param name="httpContext">Current HTTP context</param>
    /// <returns>Refresh token or null if not found</returns>
    private string? GetProviderRefreshToken(HttpContext httpContext)
    {
        // Try different provider-specific refresh token headers
        var refreshTokenHeaders = new[]
        {
            "x-ms-token-aad-refresh-token",    // Microsoft Entra ID
            "x-ms-token-google-refresh-token", // Google
            "x-ms-token-github-refresh-token"  // GitHub
        };

        foreach (var header in refreshTokenHeaders)
        {
            var token = httpContext.Request.Headers[header].FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the provider name from available token headers
    /// </summary>
    /// <param name="httpContext">Current HTTP context</param>
    /// <returns>Provider name or null if not determined</returns>
    private string? GetCurrentProvider(HttpContext httpContext)
    {
        if (!string.IsNullOrEmpty(httpContext.Request.Headers["x-ms-token-aad-access-token"].FirstOrDefault()))
            return "Microsoft";
        
        if (!string.IsNullOrEmpty(httpContext.Request.Headers["x-ms-token-google-access-token"].FirstOrDefault()))
            return "Google";
        
        if (!string.IsNullOrEmpty(httpContext.Request.Headers["x-ms-token-github-access-token"].FirstOrDefault()))
            return "GitHub";
        
        if (!string.IsNullOrEmpty(httpContext.Request.Headers["x-ms-token-facebook-access-token"].FirstOrDefault()))
            return "Facebook";
        
        if (!string.IsNullOrEmpty(httpContext.Request.Headers["x-ms-token-twitter-access-token"].FirstOrDefault()))
            return "Twitter";

        return null;
    }

    /// <summary>
    /// Check if the JWT access token is expired or will expire soon
    /// </summary>
    /// <param name="accessToken">JWT access token</param>
    /// <returns>True if token is expired or will expire within 5 minutes</returns>
    private async Task<bool> IsTokenExpiredAsync(string accessToken)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
                return true;

            // Parse JWT token to get expiration
            var tokenParts = accessToken.Split('.');
            if (tokenParts.Length != 3)
            {
                await _userSettingsService.LogAuthWarnAsync("Invalid JWT token format", new { tokenPartsCount = tokenParts.Length });
                return true;
            }

            // Decode the payload (second part)
            var payload = tokenParts[1];
            
            // Add padding if needed for base64 decoding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var jsonString = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            if (tokenData?.TryGetValue("exp", out var expElement) == true)
            {
                var expUnix = expElement.GetInt64();
                var expiration = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var now = DateTimeOffset.UtcNow;
                
                // Consider token expired if it expires within 5 minutes
                var isExpired = expiration <= now.AddMinutes(5);
                
                await _userSettingsService.LogAuthDebugAsync("Token expiration check", new { 
                    expiration = expiration.ToString(),
                    now = now.ToString(),
                    minutesRemaining = (expiration - now).TotalMinutes,
                    isExpired
                });
                
                return isExpired;
            }

            await _userSettingsService.LogAuthWarnAsync("No expiration claim found in token");
            return true; // Assume expired if we can't determine expiration
        }
        catch (Exception ex)
        {
            await _userSettingsService.LogAuthErrorAsync("Error checking token expiration", new { 
                error = ex.Message,
                type = ex.GetType().Name
            });
            _logger.LogWarning(ex, "Error checking token expiration, assuming expired");
            return true; // Assume expired on error
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

            // Add the current access token as Authorization header for refresh (like JS implementation)
            var currentAccessToken = GetProviderAccessToken(httpContext);
            if (!string.IsNullOrEmpty(currentAccessToken))
            {
                refreshRequest.Headers.Add("Authorization", $"Bearer {currentAccessToken}");
            }

            // Add cache control to ensure fresh data
            refreshRequest.Headers.Add("Cache-Control", "no-cache, no-store");

            await _userSettingsService.LogAuthDebugAsync("Sending token refresh request", new { refreshUrl, hasCookies, hasAccessToken = !string.IsNullOrEmpty(currentAccessToken) });
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
                
                // Clear cached user since token was refreshed
                ClearUserCache();
                
                // Update last login time in user settings
                await _userSettingsService.UpdateLastLoginAtAsync();
            }
            else
            {
                var errorContent = await refreshResponse.Content.ReadAsStringAsync();
                await _userSettingsService.LogAuthWarnAsync("Token refresh failed", new { 
                    statusCode = refreshResponse.StatusCode,
                    errorContent = !string.IsNullOrEmpty(errorContent) ? errorContent : "No error content"
                });
                _logger.LogDebug("Token refresh failed with status: {StatusCode}, content: {ErrorContent}", 
                    refreshResponse.StatusCode, errorContent);
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
