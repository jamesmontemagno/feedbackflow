using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using SharedDump.Services.Interfaces;
using SharedDump.AI;
using FeedbackFunctions.Utils;
using FeedbackFunctions.Services;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Services.Account;
using SharedDump.Services;
using SharedDump.Models.Account;
using FeedbackFunctions.Services.Reports;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for managing report requests
/// </summary>
public class ReportRequestFunctions
{
    private readonly ILogger<ReportRequestFunctions> _logger;
    private readonly IConfiguration _configuration;
    private const string TableName = "reportrequests";
    private const string UserRequestsTableName = "userreportrequests";
    private readonly TableClient _tableClient;
    private readonly TableClient _userRequestsTableClient;
    private readonly BlobServiceClient _serviceClient; // Still needed for reports filtering
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ReportGenerator _reportGenerator;
    private readonly IReportCacheService _cacheService;
    private readonly IRedditService _redditService;
    private readonly IGitHubService _githubService;
    private readonly FeedbackFunctions.Middleware.AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;

    public ReportRequestFunctions(
        ILogger<ReportRequestFunctions> logger,
        IConfiguration configuration,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        IReportCacheService cacheService,
        FeedbackFunctions.Middleware.AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService)
    {
#if DEBUG
        _configuration = new ConfigurationBuilder()
                    .AddJsonFile("local.settings.json")
                    .AddUserSecrets<Program>()
                    .Build();
#else
        _configuration = configuration;
#endif
        _logger = logger;
        _cacheService = cacheService;
        _redditService = redditService;
        _githubService = githubService;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
        
        // Initialize table client
        var storageConnection = _configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
        var tableServiceClient = new TableServiceClient(storageConnection);
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();

        // Initialize user-specific requests table client
        _userRequestsTableClient = tableServiceClient.GetTableClient(UserRequestsTableName);
        _userRequestsTableClient.CreateIfNotExists();

        // Initialize blob service client for reports filtering
        _serviceClient = new BlobServiceClient(storageConnection);

        // Initialize report generator
        var reportsContainerClient = _serviceClient.GetBlobContainerClient("reports");
        reportsContainerClient.CreateIfNotExists();
        _reportGenerator = new ReportGenerator(_logger, redditService, githubService, analyzerService, reportsContainerClient, _cacheService);
    }

    private static string GenerateRequestId(ReportRequestModel request)
    {
        var source = request.Type.ToLowerInvariant();
        var identifier = request.Type == "reddit"
            ? request.Subreddit?.ToLowerInvariant()
            : $"{request.Owner?.ToLowerInvariant()}/{request.Repo?.ToLowerInvariant()}";

        return $"{source}_{identifier}".Replace("/", "_");
    }

    private static string GetPartitionKeyFromId(string id)
    {
        // ID format is "{type}_{identifier}", so we extract the type part
        var parts = id.Split('_', 2);
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    // ==============================================
    // USER-SPECIFIC REPORT REQUEST FUNCTIONS
    // ==============================================

    /// <summary>
    /// Add a report request for a specific user
    /// </summary>
    [Function("AddUserReportRequest")]
    [Authorize]
    public async Task<HttpResponseData> AddUserReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Adding user-specific report request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.ReportCreated, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

        if (user == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("User authentication failed");
            return errorResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UserReportRequestModel>(requestBody, _jsonOptions);

            if (request == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request data");
                return badRequestResponse;
            }

            // Set user ID from authenticated user
            request.UserId = user.UserId;

            // Validate required fields
            if (string.IsNullOrEmpty(request.Type))
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await validationResponse.WriteStringAsync("Source type is required");
                return validationResponse;
            }

            // Validate fields based on type
            if (request.Type == "reddit")
            {
                if (string.IsNullOrEmpty(request.Subreddit))
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync("Subreddit is required for Reddit reports");
                    return validationResponse;
                }
                
                // Validate subreddit name
                var subredditValidation = UrlValidationService.ValidateSubredditName(request.Subreddit);
                if (!subredditValidation.IsValid)
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync($"Invalid subreddit name: {subredditValidation.ErrorMessage}");
                    return validationResponse;
                }

                // Check if subreddit is valid using Reddit API
                var subredditExists = await _redditService.CheckSubredditValid(request.Subreddit);
                if (!subredditExists)
                {
                    _logger.LogWarning($"Subreddit does not exist or is not accessible: {request.Subreddit}.");
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync($"Subreddit does not exist or is not accessible: {request.Subreddit}.");
                    return validationResponse;
                }
            }
            else if (request.Type == "github")
            {
                if (string.IsNullOrEmpty(request.Owner) || string.IsNullOrEmpty(request.Repo))
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync("Owner and repository are required for GitHub reports");
                    return validationResponse;
                }
                
                // Validate owner name
                var ownerValidation = UrlValidationService.ValidateGitHubOwnerName(request.Owner);
                if (!ownerValidation.IsValid)
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync($"Invalid GitHub owner name: {ownerValidation.ErrorMessage}");
                    return validationResponse;
                }
                
                // Validate repository name
                var repoValidation = UrlValidationService.ValidateGitHubRepoName(request.Repo);
                if (!repoValidation.IsValid)
                {
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync($"Invalid GitHub repository name: {repoValidation.ErrorMessage}");
                    return validationResponse;
                }

                // Check if GitHub repository is valid using GitHub API
                var repoExists = await _githubService.CheckRepositoryValid(request.Owner, request.Repo);
                if (!repoExists)
                {
                    _logger.LogWarning($"GitHub repository does not exist or is not accessible: {request.Owner}/{request.Repo}.");
                    var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await validationResponse.WriteStringAsync($"GitHub repository does not exist or is not accessible: {request.Owner}/{request.Repo}.");
                    return validationResponse;
                }
            }
            else
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await validationResponse.WriteStringAsync("Invalid source type. Must be 'reddit' or 'github'");
                return validationResponse;
            }

            // Validate email notification settings
            if (request.EmailEnabled)
            {
                // Get user account to validate tier permissions
                var userAccount = await _userAccountService.GetUserAccountAsync(user.UserId);
                if (userAccount == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Unable to retrieve user account information");
                    return errorResponse;
                }

                // Check if user's tier supports email notifications
                if (!SharedDump.Utils.Account.AccountTierUtils.SupportsEmailNotifications(userAccount.Tier))
                {
                    var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbiddenResponse.WriteStringAsync("Email notifications are not available for your current account tier. Please upgrade to Pro or Pro+ to enable email notifications.");
                    return forbiddenResponse;
                }
            }

            // Generate deterministic ID based on type and parameters
            var requestId = GenerateUserRequestId(request);
            request.Id = requestId;
            
            // Set table entity properties
            request.PartitionKey = request.UserId;
            request.RowKey = requestId;

            try
            {
                // Check if this user already has this request
                var existingEntities = _userRequestsTableClient.QueryAsync<UserReportRequestModel>(
                    filter: $"PartitionKey eq '{request.PartitionKey}' and RowKey eq '{request.RowKey}'",
                    maxPerPage: 1);

                UserReportRequestModel? existingEntity = null;
                await foreach (var entity in existingEntities)
                {
                    existingEntity = entity;
                    break;
                }
                
                if (existingEntity != null)
                {
                    var duplicateResponse = req.CreateResponse(HttpStatusCode.Conflict);
                    await duplicateResponse.WriteStringAsync("This request already exists for the user");
                    return duplicateResponse;
                }

                // Add the user request
                await _userRequestsTableClient.AddEntityAsync(request);
                
                _logger.LogInformation("Created new user report request {RequestId} for user {UserId}, {Type}: {Details}", 
                    request.Id, request.UserId, request.Type, 
                    request.Type == "reddit" ? request.Subreddit : $"{request.Owner}/{request.Repo}");

                // Also add/update the global request for background processing
                var globalRequest = new ReportRequestModel
                {
                    Type = request.Type,
                    Subreddit = request.Subreddit,
                    Owner = request.Owner,
                    Repo = request.Repo
                };
                
                var globalRequestId = GenerateRequestId(globalRequest);
                globalRequest.Id = globalRequestId;
                globalRequest.PartitionKey = globalRequest.Type.ToLowerInvariant();
                globalRequest.RowKey = globalRequestId;

                // Check if global request exists and increment subscriber count
                var globalExistingEntities = _tableClient.QueryAsync<ReportRequestModel>(
                    filter: $"PartitionKey eq '{globalRequest.PartitionKey}' and RowKey eq '{globalRequest.RowKey}'",
                    maxPerPage: 1);

                ReportRequestModel? globalExistingEntity = null;
                await foreach (var entity in globalExistingEntities)
                {
                    globalExistingEntity = entity;
                    break;
                }
                
                if (globalExistingEntity != null)
                {
                    globalExistingEntity.SubscriberCount++;
                    await _tableClient.UpdateEntityAsync(globalExistingEntity, globalExistingEntity.ETag);
                }
                else
                {
                    await _tableClient.AddEntityAsync(globalRequest);
                    
                    // Start report generation in the background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogInformation("Starting background report generation for new global request {RequestId}", globalRequest.Id);
                            var generatedReport = await _reportGenerator.ProcessReportRequestAsync(globalRequest);
                            
                            if (generatedReport != null)
                            {
                                _logger.LogInformation("Successfully generated background report {ReportId} for request {RequestId}", 
                                    generatedReport.Id, globalRequest.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to generate background report for request {RequestId}", globalRequest.Id);
                            }
                        }
                        catch (Exception reportEx)
                        {
                            _logger.LogError(reportEx, "Error generating background report for request {RequestId}", globalRequest.Id);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user report request {RequestId}", request.Id);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error processing request: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                id = request.Id, 
                message = "User request added successfully"
            }));
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.ReportCreated, _userAccountService, _logger, request.Type);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user report request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error adding request: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Remove a user's report request by ID
    /// </summary>
    [Function("RemoveUserReportRequest")]
    [Authorize]
    public async Task<HttpResponseData> RemoveUserReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "userreportrequest/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Removing user report request {RequestId}", id);

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.ReportDeleted, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

        if (user == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("User authentication failed");
            return errorResponse;
        }

        try
        {
            var partitionKey = user.UserId;
            var rowKey = id;

            try
            {
                // Check if user request exists
                var existingEntities = _userRequestsTableClient.QueryAsync<UserReportRequestModel>(
                    filter: $"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'",
                    maxPerPage: 1);

                UserReportRequestModel? existingEntity = null;
                await foreach (var entity in existingEntities)
                {
                    existingEntity = entity;
                    break;
                }
                
                if (existingEntity == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Request with ID {id} not found for this user");
                    return notFoundResponse;
                }

                // Remove the user request
                await _userRequestsTableClient.DeleteEntityAsync(partitionKey, rowKey);
                
                // Also decrement global request subscriber count
                var globalRequest = new ReportRequestModel
                {
                    Type = existingEntity.Type,
                    Subreddit = existingEntity.Subreddit,
                    Owner = existingEntity.Owner,
                    Repo = existingEntity.Repo
                };
                
                var globalRequestId = GenerateRequestId(globalRequest);
                var globalPartitionKey = globalRequest.Type.ToLowerInvariant();

                var globalExistingEntities = _tableClient.QueryAsync<ReportRequestModel>(
                    filter: $"PartitionKey eq '{globalPartitionKey}' and RowKey eq '{globalRequestId}'",
                    maxPerPage: 1);

                ReportRequestModel? globalExistingEntity = null;
                await foreach (var entity in globalExistingEntities)
                {
                    globalExistingEntity = entity;
                    break;
                }
                
                if (globalExistingEntity != null)
                {
                    if (globalExistingEntity.SubscriberCount > 1)
                    {
                        globalExistingEntity.SubscriberCount--;
                        await _tableClient.UpdateEntityAsync(globalExistingEntity, globalExistingEntity.ETag);
                    }
                    else
                    {
                        await _tableClient.DeleteEntityAsync(globalPartitionKey, globalRequestId);
                    }
                }
                
                _logger.LogInformation("Deleted user report request {RequestId} for user {UserId}", id, user.UserId);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Request with ID {id} not found for this user");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User request removed successfully");
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.ReportDeleted, _userAccountService, _logger, id);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user report request {RequestId}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error removing request: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Update email notification settings for a specific user report request
    /// </summary>
    [Function("UpdateUserReportRequestEmailSettings")]
    [Authorize]
    public async Task<HttpResponseData> UpdateUserReportRequestEmailSettings(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "userreportrequest/{id}/email-settings")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Updating email settings for user report request {RequestId}", id);

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("User authentication failed");
            return errorResponse;
        }

        try
        {
            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Request body is required");
                return badRequestResponse;
            }

            var emailSettings = JsonSerializer.Deserialize<EmailSettingsUpdate>(requestBody, _jsonOptions);
            if (emailSettings == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body format");
                return badRequestResponse;
            }

            var partitionKey = user.UserId;
            var rowKey = id;

            // Check if user request exists
            var existingEntities = _userRequestsTableClient.QueryAsync<UserReportRequestModel>(
                filter: $"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'",
                maxPerPage: 1);

            UserReportRequestModel? existingEntity = null;
            await foreach (var entity in existingEntities)
            {
                existingEntity = entity;
                break;
            }
            
            if (existingEntity == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Request with ID {id} not found for this user");
                return notFoundResponse;
            }

            // Get user account to validate tier permissions
            var userAccount = await _userAccountService.GetUserAccountAsync(user.UserId);
            if (userAccount == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Unable to retrieve user account information");
                return errorResponse;
            }

            // Validate email notification permissions (prevent Free tier users from enabling notifications)
            var emailEnabled = emailSettings.EmailEnabled;
            if (emailEnabled && !SharedDump.Utils.Account.AccountTierUtils.SupportsEmailNotifications(userAccount.Tier))
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteStringAsync("Email notifications are not available for your current account tier. Please upgrade to Pro or Pro+ to enable email notifications.");
                return forbiddenResponse;
            }

            // Update the email settings
            existingEntity.EmailEnabled = emailEnabled;

            // Update the entity in table storage
            await _userRequestsTableClient.UpdateEntityAsync(existingEntity, existingEntity.ETag);
            
            _logger.LogInformation("Updated email settings for user report request {RequestId} for user {UserId}: EmailEnabled={EmailEnabled}", 
                id, user.UserId, emailEnabled);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Email settings updated successfully");
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync($"Request with ID {id} not found for this user");
            return notFoundResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing email settings update request");
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Invalid request body format");
            return badRequestResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email settings for user report request {RequestId}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error updating email settings: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// List all report requests for a specific user
    /// </summary>
    [Function("ListUserReportRequests")]
    [Authorize]
    public async Task<HttpResponseData> ListUserReportRequests(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Listing user report requests");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await errorResponse.WriteStringAsync("User authentication failed");
            return errorResponse;
        }

        try
        {
            var requests = new List<UserReportRequestModel>();
            
            // Query for this user's requests
            await foreach (var entity in _userRequestsTableClient.QueryAsync<UserReportRequestModel>(
                filter: $"PartitionKey eq '{user.UserId}'"))
            {
                requests.Add(entity);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { requests }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing user report requests");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error listing requests: {ex.Message}");
            return errorResponse;
        }
    }

    private static string GenerateUserRequestId(UserReportRequestModel request)
    {
        var source = request.Type.ToLowerInvariant();
        var identifier = request.Type == "reddit"
            ? request.Subreddit?.ToLowerInvariant()
            : $"{request.Owner?.ToLowerInvariant()}/{request.Repo?.ToLowerInvariant()}";

        return $"{source}_{identifier}".Replace("/", "_");
    }

    /// <summary>
    /// Model for updating email notification settings for a specific report
    /// </summary>
    public class EmailSettingsUpdate
    {
        public bool EmailEnabled { get; set; }
    }
}