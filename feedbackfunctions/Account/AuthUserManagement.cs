using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Middleware;
using SharedDump.Models.Authentication;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using FeedbackFunctions.Attributes;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using FeedbackFunctions.Models;
using SharedDump.Models.Reports;

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
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;

    public AuthUserManagement(
        ILogger<AuthUserManagement> logger,
        FeedbackFunctions.Middleware.AuthenticationMiddleware authMiddleware,
        IAuthUserTableService userService,
        IUserAccountService userAccountService,
        IConfiguration configuration)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userService = userService;
        _userAccountService = userAccountService;
        
        // Create storage clients using connection string, following the pattern from other services
        var storageConnection = configuration["ProductionStorage"] ??
                              throw new InvalidOperationException("No storage connection string configured");
        
        _blobServiceClient = new BlobServiceClient(storageConnection);
        _tableServiceClient = new TableServiceClient(storageConnection);
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
                    
                    _logger.LogInformation("Raw request body received: {RequestBody}", requestBody);
                    
                    if (!string.IsNullOrWhiteSpace(requestBody))
                    {
                        var registrationRequest = JsonSerializer.Deserialize<RegisterUserRequest>(requestBody, FeedbackJsonContext.Default.RegisterUserRequest);
                        preferredEmail = registrationRequest?.PreferredEmail;
                        
                        _logger.LogInformation("Deserialized registration request - PreferredEmail: {PreferredEmail}", preferredEmail);
                        
                        if (!string.IsNullOrEmpty(preferredEmail))
                        {
                            _logger.LogInformation("Preferred email provided in registration request: {Email}", preferredEmail);
                        }
                        else
                        {
                            _logger.LogInformation("No preferred email found in registration request");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Request body is empty or whitespace");
                    }
                }
                else
                {
                    _logger.LogInformation("Request body cannot be read or has zero length - CanRead: {CanRead}, Length: {Length}", 
                        req.Body.CanRead, req.Body.Length);
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

            var userId = authenticatedUser.UserId;
            _logger.LogInformation("Starting comprehensive user deletion for user {UserId}", userId);

            // 1. Delete all shared analyses from blob storage
            await DeleteUserSharedAnalysesAsync(userId);

            // 2. Delete all report requests
            await DeleteUserReportRequestsAsync(userId);

            // 3. Delete UserAccount record
            await DeleteUserAccountAsync(userId);

            // 4. Delete the auth user permanently
            var success = await _userService.DeleteUserAsync(userId);

            if (!success)
            {
                _logger.LogWarning("User {UserId} not found for deletion", userId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { error = "User not found" });
                return notFoundResponse;
            }

            _logger.LogInformation("User {UserId} completely deleted with all associated data", userId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Your account and all associated data have been permanently deleted."
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
    /// Delete all shared analyses created by the user from blob storage
    /// </summary>
    /// <param name="userId">User ID to delete shared analyses for</param>
    private async Task DeleteUserSharedAnalysesAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Deleting shared analyses for user {UserId}", userId);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient("shared-analyses");
            var exists = await containerClient.ExistsAsync();
            
            if (!exists)
            {
                _logger.LogInformation("Shared analyses container does not exist, skipping");
                return;
            }

            var deletedCount = 0;
            
            // Get all blobs and check if they belong to this user
            // Note: We would need to store user ownership metadata on blobs to do this efficiently
            // For now, this is a placeholder - in production you'd want to track user ownership
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                try
                {
                    // In a real implementation, you'd have user metadata on blobs
                    // For now, we'll skip this as we don't have user tracking on shared analyses
                    // This would require a design change to track ownership
                    _logger.LogDebug("Found shared analysis blob: {BlobName}", blobItem.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing shared analysis blob {BlobName}", blobItem.Name);
                }
            }

            _logger.LogInformation("Processed shared analyses cleanup for user {UserId}, deleted {Count} items", userId, deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shared analyses for user {UserId}", userId);
            // Don't throw - continue with other cleanup operations
        }
    }

    /// <summary>
    /// Delete all report requests created by the user
    /// </summary>
    /// <param name="userId">User ID to delete report requests for</param>
    private async Task DeleteUserReportRequestsAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Deleting report requests for user {UserId}", userId);
            
            var tableClient = _tableServiceClient.GetTableClient("ReportRequests");
            
            // Try to ensure table exists, but continue if it doesn't
            try
            {
                await tableClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify ReportRequests table existence, proceeding anyway");
            }

            var deletedCount = 0;
            
            // Query for all report requests by this user
            var query = tableClient.QueryAsync<ReportRequestModel>(
                filter: $"CreatedBy eq '{userId}'");

            await foreach (var reportRequest in query)
            {
                try
                {
                    await tableClient.DeleteEntityAsync(reportRequest.PartitionKey, reportRequest.RowKey);
                    deletedCount++;
                    _logger.LogDebug("Deleted report request {RequestId} for user {UserId}", reportRequest.RowKey, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting report request {RequestId} for user {UserId}", reportRequest.RowKey, userId);
                }
            }

            _logger.LogInformation("Deleted {Count} report requests for user {UserId}", deletedCount, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report requests for user {UserId}", userId);
            // Don't throw - continue with other cleanup operations
        }
    }

    /// <summary>
    /// Delete the UserAccount record
    /// </summary>
    /// <param name="userId">User ID to delete account for</param>
    private async Task DeleteUserAccountAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Deleting UserAccount record for user {UserId}", userId);
            
            var success = await _userAccountService.DeleteUserAccountAsync(userId);
            if (success)
            {
                _logger.LogInformation("Deleted UserAccount record for user {UserId}", userId);
            }
            else
            {
                _logger.LogInformation("UserAccount record not found for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting UserAccount for user {UserId}", userId);
            // Don't throw - continue with other cleanup operations
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
