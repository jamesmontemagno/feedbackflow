using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;

namespace FeedbackFunctions.Utils;

public static class ApiKeyValidationHelper
{
    public static async Task<(bool IsValid, HttpResponseData? ErrorResponse)> ValidateApiKeyAsync(
        HttpRequestData req, 
        IApiKeyService apiKeyService, 
        ILogger logger)
    {
        // Check for API key in header
        var apiKeyHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("x-api-key", StringComparison.OrdinalIgnoreCase));
        if (!apiKeyHeader.Key.Any() || !apiKeyHeader.Value.Any())
        {
            // Check for API key in query parameter
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var apiKey = queryParams["apikey"] ?? queryParams["api_key"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogWarning("API key missing from request to {Path}", req.Url.AbsolutePath);
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await errorResponse.WriteStringAsync("API key is required. Provide it in 'x-api-key' header or 'apikey' query parameter.");
                return (false, errorResponse);
            }

            return await ValidateKeyAsync(req, apiKeyService, logger, apiKey);
        }

        var headerApiKey = apiKeyHeader.Value.First();
        return await ValidateKeyAsync(req, apiKeyService, logger, headerApiKey);
    }

    private static async Task<(bool IsValid, HttpResponseData? ErrorResponse)> ValidateKeyAsync(
        HttpRequestData req, 
        IApiKeyService apiKeyService, 
        ILogger logger, 
        string apiKey)
    {
        var isValid = await apiKeyService.ValidateApiKeyAsync(apiKey);
        if (!isValid)
        {
            logger.LogWarning("Invalid or disabled API key used for request to {Path}", req.Url.AbsolutePath);
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("Invalid or disabled API key. Contact admin to enable your API key.");
            return (false, errorResponse);
        }

        // Update last used timestamp (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await apiKeyService.UpdateLastUsedAsync(apiKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update last used timestamp for API key");
            }
        });

        return (true, null);
    }
}