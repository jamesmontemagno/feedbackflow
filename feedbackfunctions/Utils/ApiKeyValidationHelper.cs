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
        try
        {
            logger.LogInformation("Starting API key validation for {Path}", req.Url.AbsolutePath);
            
            // Log request details for debugging
            logger.LogInformation("Request URL: {Url}", req.Url.ToString());
            logger.LogInformation("Request headers count: {HeaderCount}", req.Headers.Count());
            
            // Check for API key in header
            var apiKeyHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("x-api-key", StringComparison.OrdinalIgnoreCase));
            string? apiKey = null;
            
            logger.LogInformation("Checking for API key in headers...");
            if (string.IsNullOrEmpty(apiKeyHeader.Key) || !apiKeyHeader.Value.Any())
            {
                logger.LogInformation("No API key found in headers, checking query parameters...");
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
                else
                {
                    logger.LogInformation("API key found in query parameter (length: {Length})", apiKey.Length);
                }
            }
            else
            {
                apiKey = apiKeyHeader.Value.First();
                logger.LogInformation("API key found in header (length: {Length})", apiKey.Length);
            }

            logger.LogInformation("Proceeding to validate API key with usage check...");
            return await ValidateKeyWithUsageAsync(req, apiKeyService, userAccountService, logger, apiKey, apiUsageAmount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during API key validation for {Path}", req.Url.AbsolutePath);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal error during API key validation.");
            return (false, errorResponse, null);
        }
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
        try
        {
            logger.LogInformation("Validating API key with service (key prefix: {Prefix})", apiKey.Length > 5 ? apiKey.Substring(0, 5) : apiKey);
            
            var isValid = await apiKeyService.ValidateApiKeyAsync(apiKey);
            if (!isValid)
            {
                logger.LogWarning("Invalid or disabled API key used for request to {Path}", req.Url.AbsolutePath);
                var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await errorResponse.WriteStringAsync("Invalid or disabled API key. Contact admin to enable your API key.");
                return (false, errorResponse, null);
            }

            logger.LogInformation("API key is valid, getting user ID...");
            
            // Get user ID for usage tracking
            var userId = await apiKeyService.GetUserIdByApiKeyAsync(apiKey);
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogError("Could not find user ID for valid API key");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Internal error processing API key.");
                return (false, errorResponse, null);
            }

            logger.LogInformation("Found user ID: {UserId}, checking usage limits (usage amount: {Amount})", userId, apiUsageAmount);

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
                logger.LogInformation("Usage validation passed for user {UserId}", userId);
            }

            logger.LogInformation("Updating last used timestamp for API key...");
            
            // Update last used timestamp (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await apiKeyService.UpdateLastUsedAsync(apiKey);
                    logger.LogInformation("Successfully updated last used timestamp");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to update last used timestamp for API key");
                }
            });

            logger.LogInformation("API key validation completed successfully");
            return (true, null, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during key validation with usage check");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal error during API key validation.");
            return (false, errorResponse, null);
        }
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