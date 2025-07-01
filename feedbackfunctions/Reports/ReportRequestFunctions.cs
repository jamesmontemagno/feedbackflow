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
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;
using SharedDump.Services;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for managing report requests
/// </summary>
public class ReportRequestFunctions
{
    private readonly ILogger<ReportRequestFunctions> _logger;
    private readonly IConfiguration _configuration;
    private const string TableName = "reportrequests";
    private readonly TableClient _tableClient;
    private readonly BlobServiceClient _serviceClient; // Still needed for reports filtering
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ReportGenerator _reportGenerator;
    private readonly IReportCacheService _cacheService;
    private readonly IRedditService _redditService;
    private readonly IGitHubService _githubService;
    private readonly AuthenticationMiddleware _authMiddleware;

    public ReportRequestFunctions(
        ILogger<ReportRequestFunctions> logger,
        IConfiguration configuration,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        IReportCacheService cacheService,
        AuthenticationMiddleware authMiddleware)
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
        
        // Initialize table client
        var storageConnection = _configuration["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection string not configured");
        var tableServiceClient = new TableServiceClient(storageConnection);
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();

        // Initialize blob service client for reports filtering
        _serviceClient = new BlobServiceClient(storageConnection);

        // Initialize report generator
        var reportsContainerClient = _serviceClient.GetBlobContainerClient("reports");
        reportsContainerClient.CreateIfNotExists();
        _reportGenerator = new ReportGenerator(_logger, redditService, githubService, analyzerService, reportsContainerClient, _cacheService);
    }

    /// <summary>
    /// Add a new report request
    /// </summary>
    [Function("AddReportRequest")]
    [Authorize]
    public async Task<HttpResponseData> AddReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Adding new report request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<ReportRequestModel>(requestBody, _jsonOptions);

            if (request == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request data");
                return badRequestResponse;
            }

            // Validate required fields and URLs
            if (string.IsNullOrEmpty(request.Type))
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await validationResponse.WriteStringAsync("Source type is required");
                return validationResponse;
            }

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

            // Generate deterministic ID based on type and parameters
            var requestId = GenerateRequestId(request);
            request.Id = requestId;
            
            // Set table entity properties
            request.PartitionKey = request.Type.ToLowerInvariant();
            request.RowKey = requestId;

            try
            {
                // Check if entity exists using query instead of exception handling
                var existingEntities = _tableClient.QueryAsync<ReportRequestModel>(
                    filter: $"PartitionKey eq '{request.PartitionKey}' and RowKey eq '{request.RowKey}'",
                    maxPerPage: 1);

                ReportRequestModel? existingEntity = null;
                await foreach (var entity in existingEntities)
                {
                    existingEntity = entity;
                    break; // We only expect one result
                }
                
                if (existingEntity != null)
                {
                    // Entity exists, increment subscriber count
                    existingEntity.SubscriberCount++;
                    await _tableClient.UpdateEntityAsync(existingEntity, existingEntity.ETag);
                    
                    _logger.LogInformation("Incremented subscriber count for existing request {RequestId} to {Count}", 
                        request.Id, existingEntity.SubscriberCount);
                }
                else
                {
                    // Entity doesn't exist, create new one
                    await _tableClient.AddEntityAsync(request);
                    
                    _logger.LogInformation("Created new report request {RequestId} for {Type}: {Details}", 
                        request.Id, request.Type, 
                        request.Type == "reddit" ? request.Subreddit : $"{request.Owner}/{request.Repo}");

                    // Start report generation in the background without waiting for it
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogInformation("Starting background report generation for new request {RequestId}", request.Id);
                            var generatedReport = await _reportGenerator.ProcessReportRequestAsync(request);
                            
                            if (generatedReport != null)
                            {
                                _logger.LogInformation("Successfully generated background report {ReportId} for request {RequestId}", 
                                    generatedReport.Id, request.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to generate background report for request {RequestId}", request.Id);
                            }
                        }
                        catch (Exception reportEx)
                        {
                            _logger.LogError(reportEx, "Error generating background report for request {RequestId}", request.Id);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking/creating report request {RequestId}", request.Id);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error processing request: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                id = request.Id, 
                message = "Request added successfully"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding report request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error adding request: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Remove a report request by ID
    /// </summary>
    [Function("RemoveReportRequest")]
    [Authorize]
    public async Task<HttpResponseData> RemoveReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "reportrequest/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Removing report request {RequestId}", id);

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Parse the ID to get partition key and row key
            var partitionKey = GetPartitionKeyFromId(id);
            var rowKey = id;

            try
            {
                // Check if entity exists using query instead of exception handling
                var existingEntities = _tableClient.QueryAsync<ReportRequestModel>(
                    filter: $"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'",
                    maxPerPage: 1);

                ReportRequestModel? existingEntity = null;
                await foreach (var entity in existingEntities)
                {
                    existingEntity = entity;
                    break; // We only expect one result
                }
                
                if (existingEntity == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Request with ID {id} not found");
                    return notFoundResponse;
                }
                
                if (existingEntity.SubscriberCount > 1)
                {
                    // Decrement subscriber count instead of deleting
                    existingEntity.SubscriberCount--;
                    await _tableClient.UpdateEntityAsync(existingEntity, existingEntity.ETag);
                    
                    _logger.LogInformation("Decremented subscriber count for request {RequestId} to {Count}", 
                        id, existingEntity.SubscriberCount);
                }
                else
                {
                    // Last subscriber, delete the request
                    await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
                    _logger.LogInformation("Deleted report request {RequestId}", id);
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Request with ID {id} not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Request removed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing report request {RequestId}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error removing request: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// List all report requests (for admin/timer trigger use)
    /// </summary>
    [Function("ListReportRequests")]
    [Authorize]
    public async Task<HttpResponseData> ListReportRequests(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Listing all report requests");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var requests = new List<ReportRequestModel>();
            
            await foreach (var entity in _tableClient.QueryAsync<ReportRequestModel>())
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
            _logger.LogError(ex, "Error listing report requests");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error listing requests: {ex.Message}");
            return errorResponse;
        }
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

}