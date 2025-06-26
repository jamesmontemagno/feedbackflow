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

    public ReportRequestFunctions(
        ILogger<ReportRequestFunctions> logger,
        IConfiguration configuration,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService)
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
        _reportGenerator = new ReportGenerator(_logger, redditService, githubService, analyzerService, reportsContainerClient);
    }

    /// <summary>
    /// Add a new report request
    /// </summary>
    [Function("AddReportRequest")]
    public async Task<HttpResponseData> AddReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Adding new report request");

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

            // Validate required fields
            if (string.IsNullOrEmpty(request.Type) || 
                (request.Type == "reddit" && string.IsNullOrEmpty(request.Subreddit)) ||
                (request.Type == "github" && (string.IsNullOrEmpty(request.Owner) || string.IsNullOrEmpty(request.Repo))))
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await validationResponse.WriteStringAsync("Missing required fields");
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
                // Try to get existing entity
                var existingEntity = await _tableClient.GetEntityAsync<ReportRequestModel>(request.PartitionKey, request.RowKey);
                
                // Entity exists, increment subscriber count
                existingEntity.Value.SubscriberCount++;
                await _tableClient.UpdateEntityAsync(existingEntity.Value, existingEntity.Value.ETag);
                
                _logger.LogInformation("Incremented subscriber count for existing request {RequestId} to {Count}", 
                    request.Id, existingEntity.Value.SubscriberCount);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Entity doesn't exist, create new one
                await _tableClient.AddEntityAsync(request);
                
                _logger.LogInformation("Created new report request {RequestId} for {Type}: {Details}", 
                    request.Id, request.Type, 
                    request.Type == "reddit" ? request.Subreddit : $"{request.Owner}/{request.Repo}");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { id = request.Id, message = "Request added successfully" }));
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
    /// Filter reports based on user's requests
    /// </summary>
    [Function("FilterReports")]
    public async Task<HttpResponseData> FilterReports(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Filtering reports based on user requests");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var userRequests = JsonSerializer.Deserialize<List<ReportRequestModel>>(requestBody, _jsonOptions);

            if (userRequests == null || !userRequests.Any())
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.OK);
                emptyResponse.Headers.Add("Content-Type", "application/json");
                await emptyResponse.WriteStringAsync(JsonSerializer.Serialize(new { reports = Array.Empty<object>() }));
                return emptyResponse;
            }

            // Get all reports from the reports container
            var reportsContainerClient = _serviceClient.GetBlobContainerClient("reports");
            var matchingReports = new List<object>();

            await foreach (var blob in reportsContainerClient.GetBlobsAsync())
            {
                var blobClient = reportsContainerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var report = JsonSerializer.Deserialize<ReportModel>(content.Value.Content, _jsonOptions);

                if (report != null)
                {
                    // Check if this report matches any of the user's requests
                    var matchesRequest = userRequests.Any(userReq =>
                        userReq.Type == report.Source &&
                        ((userReq.Type == "reddit" && userReq.Subreddit == report.SubSource) ||
                         (userReq.Type == "github" && $"{userReq.Owner}/{userReq.Repo}" == report.SubSource)));

                    if (matchesRequest)
                    {
                        matchingReports.Add(new
                        {
                            id = report.Id,
                            source = report.Source,
                            subSource = report.SubSource,
                            generatedAt = report.GeneratedAt,
                            threadCount = report.ThreadCount,
                            commentCount = report.CommentCount,
                            cutoffDate = report.CutoffDate
                        });
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { reports = matchingReports }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering reports");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error filtering reports: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Remove a report request by ID
    /// </summary>
    [Function("RemoveReportRequest")]
    public async Task<HttpResponseData> RemoveReportRequest(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "reportrequest/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Removing report request {RequestId}", id);

        try
        {
            // Parse the ID to get partition key and row key
            var partitionKey = GetPartitionKeyFromId(id);
            var rowKey = id;

            try
            {
                // Get existing entity
                var existingEntity = await _tableClient.GetEntityAsync<ReportRequestModel>(partitionKey, rowKey);
                
                if (existingEntity.Value.SubscriberCount > 1)
                {
                    // Decrement subscriber count instead of deleting
                    existingEntity.Value.SubscriberCount--;
                    await _tableClient.UpdateEntityAsync(existingEntity.Value, existingEntity.Value.ETag);
                    
                    _logger.LogInformation("Decremented subscriber count for request {RequestId} to {Count}", 
                        id, existingEntity.Value.SubscriberCount);
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
    public async Task<HttpResponseData> ListReportRequests(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Listing all report requests");

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

    /// <summary>
    /// Timer trigger for weekly report processing
    /// </summary>
    [Function("WeeklyReportProcessor")]
    public async Task WeeklyReportProcessor(
        [TimerTrigger("0 0 11 * * 1")] TimerInfo timer) // Runs Monday at 11:00 AM UTC (4:00 AM Pacific)
    {
        _logger.LogInformation("Starting weekly report processing at {Time}", DateTime.UtcNow);

        try
        {
            var requests = new List<ReportRequestModel>();
            
            await foreach (var entity in _tableClient.QueryAsync<ReportRequestModel>())
            {
                requests.Add(entity);
            }

            _logger.LogInformation("Found {RequestCount} report requests to process", requests.Count);

            foreach (var request in requests)
            {
                try
                {
                    if (request.Type == "reddit" && !string.IsNullOrEmpty(request.Subreddit))
                    {
                        _logger.LogInformation("Processing Reddit report for r/{Subreddit}", request.Subreddit);
                        var report = await _reportGenerator.GenerateRedditReportAsync(request.Subreddit);
                        _logger.LogInformation("Successfully generated Reddit report for r/{Subreddit} with ID {ReportId}", 
                            request.Subreddit, report.Id);
                    }
                    else if (request.Type == "github" && !string.IsNullOrEmpty(request.Owner) && !string.IsNullOrEmpty(request.Repo))
                    {
                        _logger.LogInformation("Processing GitHub report for {Owner}/{Repo}", request.Owner, request.Repo);
                        var report = await _reportGenerator.GenerateGitHubReportAsync(request.Owner, request.Repo);
                        _logger.LogInformation("Successfully generated GitHub report for {Owner}/{Repo} with ID {ReportId}", 
                            request.Owner, request.Repo, report.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request {RequestId}", request.Id);
                }
            }

            _logger.LogInformation("Completed weekly report processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in weekly report processing");
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