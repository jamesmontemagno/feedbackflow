using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Reports;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Reports;

/// <summary>
/// Azure Functions API for managing admin report configurations.
/// Provides CRUD operations for automated report configurations that admin users can create.
/// All endpoints require admin-level authentication and authorization.
/// 
/// Routes:
/// - GET    /api/admin/reports           - Get all admin report configurations
/// - POST   /api/admin/reports           - Create a new admin report configuration
/// - PUT    /api/admin/reports/{id}      - Update an existing admin report configuration
/// - DELETE /api/admin/reports/{id}      - Delete an admin report configuration
/// </summary>
public class AdminReportConfigFunctions
{
    private readonly ILogger<AdminReportConfigFunctions> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IAdminReportConfigService _adminReportConfigService;
    private readonly IUserAccountService _userAccountService;

    public AdminReportConfigFunctions(
        ILogger<AdminReportConfigFunctions> logger,
        AuthenticationMiddleware authMiddleware,
        IAdminReportConfigService adminReportConfigService,
        IUserAccountService userAccountService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _adminReportConfigService = adminReportConfigService;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// Get all admin report configurations (Admin only)
    /// GET /api/admin/reports
    /// </summary>
    [Function("GetAdminReportConfigs")]
    [Authorize]
    public async Task<HttpResponseData> GetAdminReportConfigsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "admin/reports")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("GetAdminReportConfigs function triggered");

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

            var configs = await _adminReportConfigService.GetAllConfigsAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(configs);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin report configs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrieving admin report configurations");
            return errorResponse;
        }
    }

    /// <summary>
    /// Create a new admin report configuration (Admin only)
    /// POST /api/admin/reports
    /// </summary>
    [Function("CreateAdminReportConfig")]
    [Authorize]
    public async Task<HttpResponseData> CreateAdminReportConfigAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/reports")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("CreateAdminReportConfig function triggered");

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

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is required");
                return badRequestResponse;
            }

            var config = JsonSerializer.Deserialize<AdminReportConfigModel>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (config == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Validate required fields
            if (string.IsNullOrEmpty(config.Name) || string.IsNullOrEmpty(config.Type) || string.IsNullOrEmpty(config.EmailRecipient))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Name, Type, and EmailRecipient are required");
                return badRequestResponse;
            }

            // Validate type-specific fields
            if (config.Type.Equals("reddit", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(config.Subreddit))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Subreddit is required for Reddit reports");
                return badRequestResponse;
            }

            if (config.Type.Equals("github", StringComparison.OrdinalIgnoreCase) && 
                (string.IsNullOrEmpty(config.Owner) || string.IsNullOrEmpty(config.Repo)))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Owner and Repo are required for GitHub reports");
                return badRequestResponse;
            }

            // Set creator
            config.CreatedBy = authenticatedUser.UserId;

            var createdConfig = await _adminReportConfigService.CreateConfigAsync(config);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(createdConfig);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin report config");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error creating admin report configuration");
            return errorResponse;
        }
    }

    /// <summary>
    /// Update an existing admin report configuration (Admin only)
    /// PUT /api/admin/reports/{id}
    /// </summary>
    [Function("UpdateAdminReportConfig")]
    [Authorize]
    public async Task<HttpResponseData> UpdateAdminReportConfigAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "admin/reports/{id}")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("UpdateAdminReportConfig function triggered for ID {Id}", id);

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

            // Check if config exists
            var existingConfig = await _adminReportConfigService.GetConfigAsync(id);
            if (existingConfig == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Admin report configuration not found");
                return notFoundResponse;
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is required");
                return badRequestResponse;
            }

            var updatedConfig = JsonSerializer.Deserialize<AdminReportConfigModel>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (updatedConfig == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Preserve important fields
            updatedConfig.Id = id;
            updatedConfig.PartitionKey = existingConfig.PartitionKey;
            updatedConfig.RowKey = existingConfig.RowKey;
            updatedConfig.CreatedAt = existingConfig.CreatedAt;
            updatedConfig.CreatedBy = existingConfig.CreatedBy;
            updatedConfig.ETag = existingConfig.ETag;

            var result = await _adminReportConfigService.UpdateConfigAsync(updatedConfig);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin report config {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error updating admin report configuration");
            return errorResponse;
        }
    }

    /// <summary>
    /// Delete an admin report configuration (Admin only)
    /// DELETE /api/admin/reports/{id}
    /// </summary>
    [Function("DeleteAdminReportConfig")]
    [Authorize]
    public async Task<HttpResponseData> DeleteAdminReportConfigAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "admin/reports/{id}")] HttpRequestData req,
        string id)
    {
        try
        {
            _logger.LogInformation("DeleteAdminReportConfig function triggered for ID {Id}", id);

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

            var success = await _adminReportConfigService.DeleteConfigAsync(id);
            if (!success)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Admin report configuration not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.NoContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting admin report config {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error deleting admin report configuration");
            return errorResponse;
        }
    }
}