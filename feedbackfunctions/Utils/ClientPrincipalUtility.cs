using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using SharedDump.Models.Authentication;

namespace FeedbackFunctions.Utils;

/// <summary>
/// Utility for extracting client principal from Azure Easy Auth requests
/// </summary>
public static class ClientPrincipalUtility
{
    /// <summary>
    /// Extract the client principal from the Azure Easy Auth headers
    /// </summary>
    /// <param name="req">HTTP request with authentication headers</param>
    /// <returns>Client principal or null if not authenticated</returns>
    public static ClientPrincipal? GetClientPrincipal(HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var principalHeaders))
            {
                return null;
            }

            var principalHeader = principalHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(principalHeader))
            {
                return null;
            }

            // Decode the base64-encoded principal
            var decoded = Convert.FromBase64String(principalHeader);
            var json = Encoding.UTF8.GetString(decoded);
            var clientPrincipal = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return clientPrincipal;
        }
        catch (Exception)
        {
            // If anything goes wrong with parsing, return null
            return null;
        }
    }

    /// <summary>
    /// Check if the request has an authenticated user
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>True if authenticated, false otherwise</returns>
    public static bool IsAuthenticated(HttpRequestData req)
    {
        var principal = GetClientPrincipal(req);
        return principal != null && !string.IsNullOrEmpty(principal.UserId);
    }

    /// <summary>
    /// Get the user's email from the client principal
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>User email or null</returns>
    public static string? GetUserEmail(HttpRequestData req)
    {
        var principal = GetClientPrincipal(req);
        return principal?.UserDetails;
    }

    /// <summary>
    /// Get the provider name from the client principal
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>Provider name or null</returns>
    public static string? GetProvider(HttpRequestData req)
    {
        var principal = GetClientPrincipal(req);
        return principal?.IdentityProvider;
    }
}
