using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Admin;

/// <summary>
/// Azure Functions API for admin dashboard metrics.
/// Provides aggregated system metrics for admin users only.
/// 
/// Routes:
/// - GET /api/GetAdminDashboardMetrics - Get comprehensive dashboard metrics (Admin only)
/// </summary>
public class AdminDashboardFunctions
{
    private readonly ILogger<AdminDashboardFunctions> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;

    public AdminDashboardFunctions(
        ILogger<AdminDashboardFunctions> logger,
        AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// Get comprehensive admin dashboard metrics (Admin only)
    /// GET /api/GetAdminDashboardMetrics
    /// </summary>
    [Function("GetAdminDashboardMetrics")]
    [Authorize]
    public async Task<HttpResponseData> GetAdminDashboardMetricsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("GetAdminDashboardMetrics function triggered");

            // Get authenticated user and verify admin access
            var authenticatedUser = await _authMiddleware.GetUserAsync(req);
            if (authenticatedUser == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Authentication required");
                return unauthorizedResponse;
            }

            // Check if user is admin
            var userAccount = await _userAccountService.GetUserAccountAsync(authenticatedUser.UserId);
            if (userAccount?.Tier != AccountTier.Admin)
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteStringAsync("Admin access required");
                return forbiddenResponse;
            }

            // Generate admin dashboard metrics
            _logger.LogInformation("Generating admin dashboard metrics for user {UserId}", authenticatedUser.UserId);
            var metrics = await _userAccountService.GetAdminDashboardMetricsAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(metrics);
            
            _logger.LogInformation("Successfully generated admin dashboard metrics. Total users: {TotalUsers}, Active users: {ActiveUsers}", 
                metrics.UserStats.TotalUsers, metrics.UserStats.ActiveUsersLast14Days);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating admin dashboard metrics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while generating dashboard metrics");
            return errorResponse;
        }
    }
}