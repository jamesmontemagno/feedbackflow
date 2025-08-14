using System.Collections.Concurrent;
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
using SharedDump.Models;
using FeedbackFunctions.Extensions;

namespace FeedbackFunctions.Account;

/// <summary>
/// Azure Functions for user management operations
/// </summary>
public class AuthUserManagement : IDisposable
{
    private readonly ILogger<AuthUserManagement> _logger;
    private readonly FeedbackFunctions.Middleware.AuthenticationMiddleware _authMiddleware;
    private readonly IAuthUserTableService _userService;
    private readonly IUserAccountService _userAccountService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly SemaphoreSlim _registrationSemaphore = new SemaphoreSlim(1, 1);
    private readonly bool _allowsRegistration;
    
    // User-specific semaphores for preventing duplicate registrations per user
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userRegistrationSemaphores = new();
    private readonly object _semaphoreCleanupLock = new object();

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
        
        // Get registration setting with default to true
        _allowsRegistration = configuration.GetValue<bool>("AllowsRegistration", true);
        
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
        // Check if registration is allowed
        if (!_allowsRegistration)
        {
            _logger.LogWarning("Registration attempt blocked - AllowsRegistration is set to false");
            var disabledResponse = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabledResponse.WriteStringAsync("Registration is currently disabled");
            return disabledResponse;
        }

        // Use user-specific semaphore to prevent double registration for the same user
        var userKey = GetUserRegistrationKey(req);
        if (string.IsNullOrEmpty(userKey))
        {
            _logger.LogWarning("Could not determine user key for registration request");
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Could not determine user identity for registration");
            return badRequestResponse;
        }

        var userSemaphore = GetOrCreateUserSemaphore(userKey);
        await userSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("RegisterUser function triggered for user key: {UserKey}", userKey);

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
            var authenticatedUser = await _authMiddleware.CreateUserAsync(req, preferredEmail);
            if (authenticatedUser == null)
            {
                _logger.LogWarning("Failed to create or authenticate user for RegisterUser request");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("User authentication or registration failed");
                return unauthorizedResponse;
            }

            // Simulate some asynchronous work
            await Task.Delay(1000);

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

                var tier = AccountTier.Free;
                if (!string.IsNullOrWhiteSpace(authenticatedUser.Email) &&
                    (authenticatedUser.Email.EndsWith("@microsoft.com", StringComparison.InvariantCultureIgnoreCase) ||
                     authenticatedUser.Email.EndsWith("@github.com", StringComparison.InvariantCultureIgnoreCase)))
                {
                    tier = AccountTier.Pro;
                }

                var templateAccount = new UserAccount
                {
                    UserId = authenticatedUser.UserId,
                    Tier = tier,
                    SubscriptionStart = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    LastResetDate = DateTime.UtcNow,
                    AnalysesUsed = 0,
                    FeedQueriesUsed = 0,
                    ActiveReports = 0,
                    PreferredEmail = finalEmail ?? string.Empty
                };

                var persistedAccount = await _userAccountService.CreateUserAccountIfNotExistsAsync(templateAccount, finalEmail);
                _logger.LogInformation("UserAccount record ready for user {UserId} (Tier: {Tier})", authenticatedUser.UserId, persistedAccount.Tier);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the registration - the UserAccount can be created later
                _logger.LogError(ex, "Failed to create UserAccount record for user {UserId}, but user registration was successful", authenticatedUser.UserId);
            }

            // Track usage for registration, outputting user info (excluding email) in resourceId
            try
            {
                var resourceId = $"userId={authenticatedUser.UserId};name={authenticatedUser.Name};authProvider={authenticatedUser.AuthProvider};providerUserId={authenticatedUser.ProviderUserId};createdAt={authenticatedUser.CreatedAt:O};lastLoginAt={authenticatedUser.LastLoginAt:O}";
                await authenticatedUser.TrackUsageAsync(
                    SharedDump.Models.Account.UsageType.Registration,
                    _userAccountService,
                    _logger,
                    resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track usage for user registration");
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
        finally
        {
            userSemaphore.Release();
            // Clean up the semaphore if it's no longer in use
            CleanupUserSemaphore(userKey);
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

            // Track usage for deletion, outputting user info (excluding email) in resourceId
            try
            {
                var resourceId = $"userId={authenticatedUser.UserId};name={authenticatedUser.Name};authProvider={authenticatedUser.AuthProvider};providerUserId={authenticatedUser.ProviderUserId};createdAt={authenticatedUser.CreatedAt:O};lastLoginAt={authenticatedUser.LastLoginAt:O}";
                await authenticatedUser.TrackUsageAsync(
                    SharedDump.Models.Account.UsageType.Deletion,
                    _userAccountService,
                    _logger,
                    resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track usage for user deletion");
            }

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
    /// Delete all shared analyses created by the user from blob storage and table storage
    /// </summary>
    /// <param name="userId">User ID to delete shared analyses for</param>
    private async Task DeleteUserSharedAnalysesAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Deleting shared analyses for user {UserId}", userId);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient("shared-analyses");
            var sharedAnalysesTableClient = _tableServiceClient.GetTableClient("SharedAnalyses");
            
            var containerExists = await containerClient.ExistsAsync();
            
            var deletedBlobCount = 0;
            var deletedTableCount = 0;
            
            // Query SharedAnalyses table to find all analyses owned by this user
            try
            {
                _logger.LogInformation("Querying SharedAnalyses table for user {UserId}", userId);
                
                await foreach (var entity in sharedAnalysesTableClient.QueryAsync<SharedAnalysisEntity>(
                    filter: $"PartitionKey eq '{userId}'"))
                {
                    try
                    {
                        // Delete the corresponding blob
                        if (containerExists)
                        {
                            var blobClient = containerClient.GetBlobClient($"{entity.Id}.json");
                            var blobExists = await blobClient.ExistsAsync();
                            if (blobExists)
                            {
                                await blobClient.DeleteAsync();
                                deletedBlobCount++;
                                _logger.LogDebug("Deleted shared analysis blob: {BlobName}", entity.Id);
                            }
                        }
                        
                        // Delete the table entity
                        await sharedAnalysesTableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                        deletedTableCount++;
                        _logger.LogDebug("Deleted shared analysis table entry: {AnalysisId}", entity.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting shared analysis {AnalysisId} for user {UserId}", entity.Id, userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Could not query SharedAnalyses table (may not exist): {Message}", ex.Message);
            }

            _logger.LogInformation("Completed shared analyses cleanup for user {UserId}, deleted {BlobCount} blobs and {TableCount} table entries", 
                userId, deletedBlobCount, deletedTableCount);
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
            
            var globalTableClient = _tableServiceClient.GetTableClient("reportrequests");
            var userTableClient = _tableServiceClient.GetTableClient("userreportrequests");
            
            // Try to ensure tables exist, but continue if they don't
            try
            {
                await globalTableClient.CreateIfNotExistsAsync();
                await userTableClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify table existence, proceeding anyway");
            }

            var deletedUserRequestsCount = 0;
            var updatedGlobalRequestsCount = 0;
            var deletedGlobalRequestsCount = 0;
            
            // First, get all user report requests to track which global requests need updating
            var userRequestsToDelete = new List<UserReportRequestModel>();
            
            try
            {
                // Query for all user report requests by this user
                var userQuery = userTableClient.QueryAsync<UserReportRequestModel>(
                    filter: $"PartitionKey eq '{userId}'");

                await foreach (var userRequest in userQuery)
                {
                    userRequestsToDelete.Add(userRequest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying user report requests for user {UserId}", userId);
            }

            // Delete user requests and update corresponding global requests
            foreach (var userRequest in userRequestsToDelete)
            {
                try
                {
                    // Delete the user request
                    await userTableClient.DeleteEntityAsync(userRequest.PartitionKey, userRequest.RowKey);
                    deletedUserRequestsCount++;
                    _logger.LogDebug("Deleted user report request {RequestId} for user {UserId}", userRequest.RowKey, userId);

                    // Find and update the corresponding global request
                    var globalRequestId = GenerateRequestId(new ReportRequestModel
                    {
                        Type = userRequest.Type,
                        Subreddit = userRequest.Subreddit,
                        Owner = userRequest.Owner,
                        Repo = userRequest.Repo
                    });

                    var globalPartitionKey = userRequest.Type.ToLowerInvariant();
                    
                    try
                    {
                        // Query for the corresponding global request
                        var globalQuery = globalTableClient.QueryAsync<ReportRequestModel>(
                            filter: $"PartitionKey eq '{globalPartitionKey}' and RowKey eq '{globalRequestId}'",
                            maxPerPage: 1);

                        ReportRequestModel? globalRequest = null;
                        await foreach (var entity in globalQuery)
                        {
                            globalRequest = entity;
                            break;
                        }

                        if (globalRequest != null)
                        {
                            if (globalRequest.SubscriberCount > 1)
                            {
                                // Decrement subscriber count
                                globalRequest.SubscriberCount--;
                                await globalTableClient.UpdateEntityAsync(globalRequest, globalRequest.ETag);
                                updatedGlobalRequestsCount++;
                                _logger.LogDebug("Decremented subscriber count for global request {RequestId} to {Count}", globalRequestId, globalRequest.SubscriberCount);
                            }
                            else
                            {
                                // Delete global request if no more subscribers
                                await globalTableClient.DeleteEntityAsync(globalRequest.PartitionKey, globalRequest.RowKey);
                                deletedGlobalRequestsCount++;
                                _logger.LogDebug("Deleted global request {RequestId} (no remaining subscribers)", globalRequestId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not find corresponding global request {RequestId} for user request", globalRequestId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error updating global request {RequestId} for user {UserId}", globalRequestId, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting user report request {RequestId} for user {UserId}", userRequest.RowKey, userId);
                }
            }

            _logger.LogInformation("Completed report request cleanup for user {UserId}: deleted {UserCount} user requests, updated {UpdatedCount} global requests, deleted {DeletedCount} global requests", 
                userId, deletedUserRequestsCount, updatedGlobalRequestsCount, deletedGlobalRequestsCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report requests for user {UserId}", userId);
            // Don't throw - continue with other cleanup operations
        }
    }

    /// <summary>
    /// Generate request ID based on request type and parameters (matches ReportRequestFunctions.cs logic)
    /// </summary>
    private static string GenerateRequestId(ReportRequestModel request)
    {
        var source = request.Type.ToLowerInvariant();
        var identifier = request.Type == "reddit"
            ? request.Subreddit?.ToLowerInvariant()
            : $"{request.Owner?.ToLowerInvariant()}/{request.Repo?.ToLowerInvariant()}";

        return $"{source}_{identifier}".Replace("/", "_");
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

    /// <summary>
    /// Get a user-specific registration key from the request headers
    /// This helps identify the same user across multiple registration attempts
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>User-specific key or null if not determinable</returns>
    private string? GetUserRegistrationKey(HttpRequestData req)
    {
        try
        {
            // Try to extract user identity from authentication headers
            if (!req.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var principalHeaders))
            {
                return null;
            }

            var principalHeader = principalHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(principalHeader))
            {
                return null;
            }

            // Decode the base64-encoded principal
            var decoded = Convert.FromBase64String(principalHeader);
            var json = System.Text.Encoding.UTF8.GetString(decoded);
            var clientPrincipal = JsonSerializer.Deserialize<SharedDump.Models.Authentication.ClientPrincipal>(json);

            if (clientPrincipal == null)
            {
                return null;
            }

            // Create a stable key based on provider and provider user ID
            var provider = clientPrincipal.GetEffectiveIdentityProvider();
            var providerUserId = clientPrincipal.GetEffectiveUserId();
            
            if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(providerUserId))
            {
                return null;
            }

            return $"{provider}:{providerUserId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user registration key from request");
            return null;
        }
    }

    /// <summary>
    /// Get or create a semaphore for a specific user
    /// </summary>
    /// <param name="userKey">User-specific key</param>
    /// <returns>Semaphore for the user</returns>
    private SemaphoreSlim GetOrCreateUserSemaphore(string userKey)
    {
        return _userRegistrationSemaphores.GetOrAdd(userKey, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Clean up user semaphore if it's no longer in use
    /// </summary>
    /// <param name="userKey">User-specific key</param>
    private void CleanupUserSemaphore(string userKey)
    {
        // Only clean up if the semaphore is not currently being waited on
        lock (_semaphoreCleanupLock)
        {
            if (_userRegistrationSemaphores.TryGetValue(userKey, out var semaphore))
            {
                // Check if semaphore is available (not being waited on)
                if (semaphore.CurrentCount > 0)
                {
                    if (_userRegistrationSemaphores.TryRemove(userKey, out var removedSemaphore))
                    {
                        removedSemaphore.Dispose();
                        _logger.LogDebug("Cleaned up semaphore for user key: {UserKey}", userKey);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        _registrationSemaphore?.Dispose();
        
        // Clean up all user semaphores
        foreach (var kvp in _userRegistrationSemaphores)
        {
            kvp.Value.Dispose();
        }
        _userRegistrationSemaphores.Clear();
    }
}
