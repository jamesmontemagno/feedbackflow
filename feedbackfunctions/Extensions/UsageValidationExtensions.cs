using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using SharedDump.Models.Authentication;

namespace FeedbackFunctions.Extensions;

/// <summary>
/// Extension methods for validating usage limits in Azure Functions
/// </summary>
public static class UsageValidationExtensions
{
    /// <summary>
    /// Validates if the authenticated user can perform the specified usage type
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="user">Authenticated user</param>
    /// <param name="usageType">Type of usage to validate</param>
    /// <param name="userAccountService">User account service</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Null if validation passes, or an error response if limit exceeded</returns>
    public static async Task<HttpResponseData?> ValidateUsageAsync(
        this HttpRequestData req,
        AuthenticatedUser user,
        UsageType usageType,
        IUserAccountService userAccountService,
        ILogger? logger = null)
    {
        try
        {
            var result = await userAccountService.ValidateUsageAsync(user.UserId, usageType);
            if (!result.IsWithinLimit)
            {
                logger?.LogWarning("Usage limit exceeded for user {UserId} and type {UsageType}", user.UserId, usageType);
                
                var errorResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                await errorResponse.WriteAsJsonAsync(new UsageValidationResult
                {
                    IsWithinLimit = false,
                    ErrorCode = "USAGE_LIMIT_EXCEEDED",
                    Message = result.ErrorMessage ?? "Usage limit exceeded.",
                    UsageType = result.UsageType,
                    CurrentUsage = result.CurrentUsage,
                    Limit = result.Limit,
                    ResetDate = usageType == UsageType.ReportCreated ? null : result.ResetDate,
                    CurrentTier = result.CurrentTier,
                    UpgradeUrl = result.UpgradeUrl,
                    ErrorMessage = result.ErrorMessage
                });
                
                return errorResponse;
            }
            
            return null; // Validation passed
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error validating usage for user {UserId} and type {UsageType}", user.UserId, usageType);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error validating usage limits");
            return errorResponse;
        }
    }

    /// <summary>
    /// Tracks usage for the authenticated user after a successful operation
    /// </summary>
    /// <param name="user">Authenticated user</param>
    /// <param name="usageType">Type of usage to track</param>
    /// <param name="userAccountService">User account service</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="resourceId">Optional resource identifier</param>
    /// <param name="amount">Amount of usage to track</param>
    public static async Task TrackUsageAsync(
        this AuthenticatedUser user,
        UsageType usageType,
        IUserAccountService userAccountService,
        ILogger? logger = null,
        string? resourceId = null,
        int amount = 1)
    {
        try
        {
            await userAccountService.TrackUsageAsync(user.UserId, usageType, resourceId, amount);
            logger?.LogInformation("Tracked usage for user {UserId} and type {UsageType}", user.UserId, usageType);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error tracking usage for user {UserId} and type {UsageType}", user.UserId, usageType);
            // Don't throw - tracking failures shouldn't break the main function
        }
    }
}
