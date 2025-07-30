using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Middleware;
using SharedDump.Models.Authentication;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions.Account;

/// <summary>
/// Azure Functions for user management operations
/// </summary>
public class AuthUserManagement
{
    private readonly ILogger<AuthUserManagement> _logger;
    private readonly FeedbackFunctions.Middleware.AuthenticationMiddleware _authMiddleware;
    private readonly IAuthUserTableService _userService;
    private readonly IUserAccountService _userAccountService;

    public AuthUserManagement(
        ILogger<AuthUserManagement> logger,
        FeedbackFunctions.Middleware.AuthenticationMiddleware authMiddleware,
        IAuthUserTableService userService,
        IUserAccountService userAccountService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userService = userService;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// Register or update a user in the system
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with user information</returns>
    [Function("RegisterUser")]
    [Authorize]
    public async Task<HttpResponseData> RegisterUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("RegisterUser function triggered");

            // Try to read preferred email from request body if provided
            string? preferredEmail = null;
            try
            {
                if (req.Body.CanRead && req.Body.Length > 0)
                {
                    req.Body.Position = 0; // Reset stream position
                    using var reader = new StreamReader(req.Body);
                    var requestBody = await reader.ReadToEndAsync();
                    
                    if (!string.IsNullOrWhiteSpace(requestBody))
                    {
                        var registrationRequest = JsonSerializer.Deserialize<RegisterUserRequest>(requestBody);
                        preferredEmail = registrationRequest?.PreferredEmail;
                        
                        if (!string.IsNullOrEmpty(preferredEmail))
                        {
                            _logger.LogInformation("Preferred email provided in registration request: {Email}", preferredEmail);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse registration request body, continuing without preferred email");
            }

            // Create or get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.CreateUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("Failed to create or authenticate user for RegisterUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication or registration failed");
                return unauthorizedResponse;
            }

            // Use preferred email if provided, otherwise fall back to authenticated user email
            var finalEmail = !string.IsNullOrEmpty(preferredEmail) ? preferredEmail : authenticatedUser.Email;
            _logger.LogInformation("Using email for registration - Preferred: {PreferredEmail}, Auth: {AuthEmail}, Final: {FinalEmail}", 
                preferredEmail, authenticatedUser.Email, finalEmail);

            // User is created by the middleware
            _logger.LogInformation("User registered successfully: {UserId}", authenticatedUser.UserId);

            // Create a new UserAccount record with default Free tier settings
            try
            {
                _logger.LogInformation("Creating UserAccount record for new user {UserId}", authenticatedUser.UserId);

                // Check if user account already exists
                var existingUserAccount = await _userAccountService.GetUserAccountAsync(authenticatedUser.UserId);
                if (existingUserAccount == null)
                {
                    // Create new user account with default Free tier
                    var userAccount = new UserAccount
                    {
                        UserId = authenticatedUser.UserId,
                        Tier = AccountTier.Free,
                        SubscriptionStart = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        LastResetDate = DateTime.UtcNow,
                        AnalysesUsed = 0,
                        FeedQueriesUsed = 0,
                        ActiveReports = 0,
                        PreferredEmail = finalEmail ?? string.Empty
                    };
                    await _userAccountService.UpsertUserAccountAsync(userAccount);
                }
                else
                {
                    // Update existing account with latest email if needed
                    if (!string.IsNullOrEmpty(finalEmail))
                    {
                        existingUserAccount.PreferredEmail = finalEmail;
                        await _userAccountService.UpsertUserAccountAsync(existingUserAccount);
                        _logger.LogInformation("Updated existing UserAccount with preferred email: {Email}", finalEmail);
                    }
                }

                _logger.LogInformation("Successfully created UserAccount record for user {UserId} with Free tier", authenticatedUser.UserId);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the registration - the UserAccount can be created later
                _logger.LogError(ex, "Failed to create UserAccount record for user {UserId}, but user registration was successful", authenticatedUser.UserId);
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
                    createdAt = authenticatedUser.CreatedAt,
                    lastLoginAt = authenticatedUser.LastLoginAt,
                    profileImageUrl = authenticatedUser.ProfileImageUrl,
                    providerUserId = authenticatedUser.ProviderUserId
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
    [Authorize]
    public async Task<HttpResponseData> DeleteUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("DeleteUser function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetUserAsync(req);
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
    [Authorize]

    public async Task<HttpResponseData> GetCurrentUserAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("GetCurrentUser function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetUserAsync(req);
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
