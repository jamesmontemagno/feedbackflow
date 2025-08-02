using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace FeedbackWebApp.Services.Authentication;

/// <summary>
/// Improved authentication header service that forwards Azure Easy Auth headers from HttpContext
/// </summary>
public class ServerSideAuthenticationHeaderService : IAuthenticationHeaderService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerSideAuthenticationHeaderService> _logger;

    public ServerSideAuthenticationHeaderService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ServerSideAuthenticationHeaderService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAuthenticationHeadersAsync(HttpRequestMessage request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext not available for adding authentication headers");
            return;
        }

        try
        {
            // Check if auth is bypassed for development
            var bypassAuth = _configuration.GetValue<bool>("Authentication:BypassInDevelopment", false);
            var isDevelopment = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";

            if (bypassAuth && isDevelopment)
            {
                // For development, create a mock authentication header
                await AddDevelopmentAuthHeaderAsync(request);
                return;
            }

            // Forward Azure Easy Auth headers if they exist
            await ForwardEasyAuthHeadersAsync(request, httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding authentication headers");
        }
    }

    /// <summary>
    /// Forward existing Azure Easy Auth headers from the current HTTP context
    /// </summary>
    private async Task ForwardEasyAuthHeadersAsync(HttpRequestMessage request, HttpContext httpContext)
    {
        // First try to get the client principal header directly from the request
        if (httpContext.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var clientPrincipalHeader))
        {
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL", clientPrincipalHeader.ToString());
            _logger.LogDebug("Forwarded X-MS-CLIENT-PRINCIPAL header from request");
            return;
        }

        // If not available, we need to get user info and create the header
        // This is a fallback for cases where Easy Auth is configured but headers aren't being forwarded
        await CreateClientPrincipalHeaderAsync(request, httpContext);
    }

    /// <summary>
    /// Create client principal header by calling /.auth/me with forwarded cookies
    /// </summary>
    private async Task CreateClientPrincipalHeaderAsync(HttpRequestMessage request, HttpContext httpContext)
    {
        try
        {
            // Create HTTP client for this specific request
            using var httpClient = new HttpClient();
            
            // Create request to /.auth/me with forwarded cookies
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var authMeUrl = $"{baseUrl}/.auth/me";

            var authRequest = new HttpRequestMessage(HttpMethod.Get, authMeUrl);
            
            // Forward all cookies from the current request
            if (httpContext.Request.Headers.ContainsKey("Cookie"))
            {
                authRequest.Headers.Add("Cookie", httpContext.Request.Headers["Cookie"].ToString());
            }

            var response = await httpClient.SendAsync(authRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Authentication check failed, no auth headers added");
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrEmpty(responseContent) || responseContent.Trim() == "[]")
            {
                _logger.LogDebug("No authenticated user found");
                return;
            }

            // Create X-MS-CLIENT-PRINCIPAL header from the response
            // Azure Functions expects this header to contain base64-encoded JSON
            var principalBytes = System.Text.Encoding.UTF8.GetBytes(responseContent);
            var principalBase64 = Convert.ToBase64String(principalBytes);

            request.Headers.Add("X-MS-CLIENT-PRINCIPAL", principalBase64);
            _logger.LogDebug("Created X-MS-CLIENT-PRINCIPAL header from /.auth/me response");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create client principal header");
        }
    }

    /// <summary>
    /// Add development authentication header for testing
    /// </summary>
    private async Task AddDevelopmentAuthHeaderAsync(HttpRequestMessage request)
    {
        var devUser = new
        {
            userId = "dev-user-id",
            userDetails = "dev@example.com",
            identityProvider = "aad",
            claims = new[]
            {
                new { type = "name", value = "Development User" },
                new { type = "email", value = "dev@example.com" },
                new { type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", value = "dev@example.com" },
                new { type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", value = "Development User" }
            }
        };

        var devUserArray = new[] { devUser };
        var devUserJson = System.Text.Json.JsonSerializer.Serialize(devUserArray);
        var devUserBytes = System.Text.Encoding.UTF8.GetBytes(devUserJson);
        var devUserBase64 = Convert.ToBase64String(devUserBytes);

        request.Headers.Add("X-MS-CLIENT-PRINCIPAL", devUserBase64);
        _logger.LogDebug("Added development authentication header");
        
        await Task.CompletedTask;
    }
}