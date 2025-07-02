using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Authentication;
using SharedDump.Services.Authentication;
using SharedDump.Models.Authentication;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for user management operations
/// </summary>
public class UserManagement
{
    private readonly ILogger<UserManagement> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IAuthUserTableService _userService;

    public UserManagement(
        ILogger<UserManagement> logger,
        AuthenticationMiddleware authMiddleware,
        IAuthUserTableService userService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userService = userService;
    }

    /// <summary>
    /// Register or update a user in the system
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with user information</returns>
    [Function("RegisterUser")]
    public async Task<HttpResponseData> RegisterUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("RegisterUser function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetOrCreateUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("No authenticated user found for RegisterUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication required");
                return unauthorizedResponse;
            }

            // User is automatically created or updated by the middleware
            _logger.LogInformation("User registered/updated successfully: {UserId}", authenticatedUser.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                user = new
                {
                    userId = authenticatedUser.UserId,
                    email = authenticatedUser.Email,
                    name = authenticatedUser.Name,
                    authProvider = authenticatedUser.AuthProvider,
                    createdAt = authenticatedUser.CreatedAt,
                    lastLoginAt = authenticatedUser.LastLoginAt
                }
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RegisterUser function");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred during user registration");
            return errorResponse;
        }
    }

    /// <summary>
    /// Delete a user account and all associated data
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response confirming deletion</returns>
    [Function("DeleteUser")]
    public async Task<HttpResponseData> DeleteUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("DeleteUser function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetOrCreateUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("No authenticated user found for DeleteUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication required");
                return unauthorizedResponse;
            }

            // Deactivate the user instead of hard deletion for data integrity
            var success = await _userService.DeactivateUserAsync(authenticatedUser.UserId);
            
            if (!success)
            {
                _logger.LogWarning("User {UserId} not found for deactivation", authenticatedUser.UserId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { error = "User not found" });
                return notFoundResponse;
            }

            _logger.LogInformation("User deactivated successfully: {UserId}", authenticatedUser.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "User account has been deactivated successfully"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteUser function");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred during user deletion");
            return errorResponse;
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with user information</returns>
    [Function("GetCurrentUser")]
    public async Task<HttpResponseData> GetCurrentUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("GetCurrentUser function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetOrCreateUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("No authenticated user found for GetCurrentUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication required");
                return unauthorizedResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                user = new
                {
                    userId = authenticatedUser.UserId,
                    email = authenticatedUser.Email,
                    name = authenticatedUser.Name,
                    authProvider = authenticatedUser.AuthProvider,
                    providerUserId = authenticatedUser.ProviderUserId,
                    profileImageUrl = authenticatedUser.ProfileImageUrl,
                    createdAt = authenticatedUser.CreatedAt,
                    lastLoginAt = authenticatedUser.LastLoginAt
                }
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCurrentUser function");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred while retrieving user information");
            return errorResponse;
        }
    }
}
