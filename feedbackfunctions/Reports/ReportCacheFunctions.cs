using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for managing report cache operations
/// </summary>
/// <remarks>
/// This class contains functions for cache management including status checking,
/// manual refresh, and cache clearing operations.
/// </remarks>
public class ReportCacheFunctions
{
    private readonly ILogger<ReportCacheFunctions> _logger;
    private readonly IReportCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the ReportCacheFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="cacheService">Report cache service for cache operations</param>
    public ReportCacheFunctions(
        ILogger<ReportCacheFunctions> logger,
        IReportCacheService cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Gets cache status information
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with cache status</returns>
    [Function("GetCacheStatus")]
    public async Task<HttpResponseData> GetCacheStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "cache/status")] HttpRequestData req)
    {
        _logger.LogInformation("Getting cache status");

        try
        {
            var status = await _cacheService.GetCacheStatusAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                reportCount = status.ReportCount,
                lastRefresh = status.LastRefresh,
                expiresAt = status.ExpiresAt,
                isExpired = DateTime.UtcNow > status.ExpiresAt
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error getting cache status: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Manually refreshes the cache from blob storage
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with refresh status</returns>
    [Function("RefreshCache")]
    public async Task<HttpResponseData> RefreshCache(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "cache/refresh")] HttpRequestData req)
    {
        _logger.LogInformation("Manual cache refresh requested");

        try
        {
            await _cacheService.RefreshCacheAsync();
            var status = await _cacheService.GetCacheStatusAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                message = "Cache refreshed successfully",
                reportCount = status.ReportCount,
                refreshedAt = status.LastRefresh
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error refreshing cache: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with clear status</returns>
    [Function("ClearCache")]
    public async Task<HttpResponseData> ClearCache(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "cache/clear")] HttpRequestData req)
    {
        _logger.LogInformation("Manual cache clear requested");

        try
        {
            await _cacheService.ClearCacheAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                message = "Cache cleared successfully"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error clearing cache: {ex.Message}");
            return errorResponse;
        }
    }
}
