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

namespace FeedbackFunctions.Account;

/// <summary>
/// Azure Functions for user management operations
/// </summary>
public class UserManagement
{
    private readonly ILogger<UserManagement> _logger;
    private readonly FeedbackFunctions.Middleware.AuthenticationMiddleware _authMiddleware;
    private readonly IAuthUserTableService _userService;
    private readonly IAccountLimitsService _limitsService;
    private readonly IUserAccountTableService _userAccountService;
    private readonly IUsageTrackingService _usageService;

    public UserManagement(
        ILogger<UserManagement> logger,
        FeedbackFunctions.Middleware.AuthenticationMiddleware authMiddleware,
        IAuthUserTableService userService,
        IAccountLimitsService limitsService,
        IUserAccountTableService userAccountService,
        IUsageTrackingService usageService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userService = userService;
        _limitsService = limitsService;
        _userAccountService = userAccountService;
        _usageService = usageService;
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

            // Create or get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.CreateUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("Failed to create or authenticate user for RegisterUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication or registration failed");
                return unauthorizedResponse;
            }

            // User is created by the middleware
            _logger.LogInformation("User registered successfully: {UserId}", authenticatedUser.UserId);

            // Create a new UserAccount record with default Free tier settings
            try
            {
                _logger.LogInformation("Creating UserAccount record for new user {UserId}", authenticatedUser.UserId);
                
                // Create new user account with default Free tier
                var userAccount = new UserAccount
                {
                    UserId = authenticatedUser.UserId,
                    Tier = AccountTier.Free,
                    AnalysesUsed = 0,
                    ActiveReports = 0,
                    FeedQueriesUsed = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastResetDate = DateTime.UtcNow,
                    SubscriptionStart = DateTime.UtcNow,
                    IsActive = true,
                    PreferredEmail = authenticatedUser.Email ?? string.Empty
                };

                // Limits are now calculated dynamically based on tier
                // Convert to entity and save to the database
                var entity = new UserAccountEntity
                {
                    PartitionKey = authenticatedUser.UserId,
                    RowKey = "account",
                    Tier = (int)userAccount.Tier,
                    AnalysesUsed = userAccount.AnalysesUsed,
                    ActiveReports = userAccount.ActiveReports,
                    FeedQueriesUsed = userAccount.FeedQueriesUsed,
                    CreatedAt = userAccount.CreatedAt,
                    LastResetDate = userAccount.LastResetDate,
                    SubscriptionStart = userAccount.SubscriptionStart,
                    SubscriptionEnd = userAccount.SubscriptionEnd,
                    IsActive = userAccount.IsActive,
                    PreferredEmail = userAccount.PreferredEmail
                };

                await _userAccountService.UpsertUserAccountAsync(entity);
                
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
                    preferredEmail = authenticatedUser.PreferredEmail,
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

    /// <summary>
    /// Update the user's preferred email address
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with success status</returns>
    [Function("UpdatePreferredEmail")]
    public async Task<HttpResponseData> UpdatePreferredEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("UpdatePreferredEmail function triggered");

            // Get authenticated user from middleware
            var authenticatedUser = await _authMiddleware.GetUserAsync(req);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("No authenticated user found for UpdatePreferredEmail request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication required");
                return unauthorizedResponse;
            }

            // Read the request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is required");
                return badRequestResponse;
            }

            // Parse the preferred email from the request
            var requestData = JsonSerializer.Deserialize<UpdatePreferredEmailRequest>(requestBody, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (requestData == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request format");
                return badRequestResponse;
            }

            // Validate email format if provided
            if (!string.IsNullOrWhiteSpace(requestData.PreferredEmail))
            {
                if (!IsValidEmail(requestData.PreferredEmail))
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid email format");
                    return badRequestResponse;
                }
            }

            // Update the user's preferred email in UserAccount
            var userAccount = await _usageService.GetUserAccountAsync();
            if (userAccount == null)
            {
                _logger.LogError("User account not found for user {UserId}", authenticatedUser.UserId);
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync("User account not found");
                return errorResponse;
            }

            // Update the preferred email
            userAccount.PreferredEmail = requestData.PreferredEmail ?? string.Empty;

            // Convert to entity and save
            var entity = new UserAccountEntity
            {
                PartitionKey = authenticatedUser.UserId,
                RowKey = "account",
                Tier = (int)userAccount.Tier,
                AnalysesUsed = userAccount.AnalysesUsed,
                ActiveReports = userAccount.ActiveReports,
                FeedQueriesUsed = userAccount.FeedQueriesUsed,
                CreatedAt = userAccount.CreatedAt,
                LastResetDate = userAccount.LastResetDate,
                SubscriptionStart = userAccount.SubscriptionStart,
                SubscriptionEnd = userAccount.SubscriptionEnd,
                IsActive = userAccount.IsActive,
                PreferredEmail = userAccount.PreferredEmail
            };

            await _userAccountService.UpsertUserAccountAsync(entity);

            _logger.LogInformation("Successfully updated preferred email for user {UserId}", authenticatedUser.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { Success = true, Message = "Preferred email updated successfully" });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdatePreferredEmail function");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error occurred while updating preferred email");
            return errorResponse;
        }
    }

    /// <summary>
    /// Simple email validation
    /// </summary>
    /// <param name="email">Email to validate</param>
    /// <returns>True if email format is valid</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Request model for updating preferred email
    /// </summary>
    private class UpdatePreferredEmailRequest
    {
        public string? PreferredEmail { get; set; }
    }
}
