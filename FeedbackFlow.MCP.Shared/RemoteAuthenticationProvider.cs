using Microsoft.AspNetCore.Http;

namespace FeedbackFlow.MCP.Shared;

/// <summary>
/// Authentication provider for remote deployment using HTTP Bearer tokens
/// </summary>
public class RemoteAuthenticationProvider : IAuthenticationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RemoteAuthenticationProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetAuthenticationToken()
    {
        if (_httpContextAccessor?.HttpContext is null)
        {
            Console.WriteLine("HttpContext is null");
            return null;
        }

        if (_httpContextAccessor.HttpContext.Request.Headers.TryGetValue("Authorization", out var token))
        {
            Console.WriteLine("Authorization header found");
            var bearerToken = token.ToString();
            return bearerToken.StartsWith("Bearer ") ? bearerToken[7..] : bearerToken;
        }

        return null;
    }

    public string GetAuthenticationErrorMessage()
    {
        return "Error: Bearer token required. Get from FeedbackFlow API documentation.";
    }
}