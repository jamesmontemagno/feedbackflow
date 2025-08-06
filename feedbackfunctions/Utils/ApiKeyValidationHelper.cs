using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Utils;

public static class ApiKeyValidationHelper
{
    public static async Task<(bool IsValid, HttpResponseData? ErrorResponse, string? UserId)> ValidateApiKeyWithUsageAsync(
        HttpRequestData req, 
        IApiKeyService apiKeyService,
        IUserAccountService userAccountService,
        ILogger logger,
        int apiUsageAmount = 1)
    {
        // Check for API key in header
        var apiKeyHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("x-api-key", StringComparison.OrdinalIgnoreCase));
        string? apiKey = null;
        
        if (!apiKeyHeader.Key.Any() || !apiKeyHeader.Value.Any())
        {
            // Check for API key in query parameter
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            apiKey = queryParams["apikey"] ?? queryParams["api_key"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogWarning("API key missing from request to {Path}", req.Url.AbsolutePath);
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await errorResponse.WriteStringAsync("API key is required. Provide it in 'x-api-key' header or 'apikey' query parameter.");
                return (false, errorResponse, null);
            }
        }
        else
        {
            apiKey = apiKeyHeader.Value.First();
        }

        return await ValidateKeyWithUsageAsync(req, apiKeyService, userAccountService, logger, apiKey, apiUsageAmount);
    }

    public static async Task<(bool IsValid, HttpResponseData? ErrorResponse)> ValidateApiKeyAsync(
        HttpRequestData req, 
        IApiKeyService apiKeyService, 
        ILogger logger)
    {
        var (isValid, errorResponse, _) = await ValidateApiKeyWithUsageAsync(req, apiKeyService, null!, logger, 0);
        return (isValid, errorResponse);
    }

    private static async Task<(bool IsValid, HttpResponseData? ErrorResponse, string? UserId)> ValidateKeyWithUsageAsync(
        HttpRequestData req, 
        IApiKeyService apiKeyService,
        IUserAccountService? userAccountService,
        ILogger logger, 
        string apiKey,
        int apiUsageAmount = 1)
    {
        var isValid = await apiKeyService.ValidateApiKeyAsync(apiKey);
        if (!isValid)
        {
            logger.LogWarning("Invalid or disabled API key used for request to {Path}", req.Url.AbsolutePath);
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("Invalid or disabled API key. Contact admin to enable your API key.");
            return (false, errorResponse, null);
        }

        // Get user ID for usage tracking
        var userId = await apiKeyService.GetUserIdByApiKeyAsync(apiKey);
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogError("Could not find user ID for valid API key");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal error processing API key.");
            return (false, errorResponse, null);
        }

        // Check usage limits if userAccountService is provided and apiUsageAmount > 0
        if (userAccountService != null && apiUsageAmount > 0)
        {
            var usageValidation = await userAccountService.ValidateUsageAsync(userId, UsageType.ApiCall);
            if (!usageValidation.IsWithinLimit)
            {
                logger.LogWarning("API usage limit exceeded for user {UserId}", userId);
                var errorResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                await errorResponse.WriteAsJsonAsync(usageValidation);
                return (false, errorResponse, userId);
            }
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

        return (true, null, userId);
    }

    public static async Task TrackApiUsageAsync(
        string userId,
        int amount,
        IUserAccountService userAccountService,
        ILogger logger,
        string? resourceId = null)
    {
        try
        {
            await userAccountService.TrackUsageAsync(userId, UsageType.ApiCall, resourceId, amount);
            logger.LogInformation("Tracked API usage for user {UserId}, amount: {Amount}", userId, amount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tracking API usage for user {UserId}", userId);
            // Don't throw - tracking failures shouldn't break the main function
        }
    }
}