using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using SharedDump.Models.Authentication;
using FeedbackFunctions.Middleware;

namespace FeedbackFunctions.Extensions;

/// <summary>
/// Extension methods for HTTP requests to handle authentication
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Authenticate and authorize the request, returning the user or an error response
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="authMiddleware">Authentication middleware</param>
    /// <returns>Either the authenticated user and null response, or null user and error response</returns>
    public static async Task<(AuthenticatedUser? user, HttpResponseData? errorResponse)> AuthenticateAsync(
        this HttpRequestData req, 
        AuthenticationMiddleware authMiddleware)
    {
        try
        {
            var user = await authMiddleware.GetUserAsync(req);
            if (user == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await errorResponse.WriteStringAsync("Authentication required");
                return (null, errorResponse);
            }

            return (user, null);
        }
        catch (Exception)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Authentication error");
            return (null, errorResponse);
        }
    }
}