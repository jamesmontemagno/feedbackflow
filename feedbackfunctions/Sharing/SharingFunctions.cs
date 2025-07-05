using System.Net;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models;
using System.Text;
using System.Text.Json;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions;

/// <summary>
/// Request model for saving shared analysis with privacy settings
/// </summary>
public class SaveAnalysisRequest
{
    /// <summary>
    /// The analysis data to save
    /// </summary>
    public AnalysisData Analysis { get; set; } = new();

    /// <summary>
    /// Whether the analysis should be publicly accessible
    /// </summary>
    public bool IsPublic { get; set; } = false;
}

/// <summary>
/// Request model for updating analysis visibility
/// </summary>
public class UpdateVisibilityRequest
{
    /// <summary>
    /// Whether the analysis should be publicly accessible
    /// </summary>
    public bool IsPublic { get; set; } = false;
}

/// <summary>
/// Azure Functions for sharing and retrieving analysis results
/// </summary>
/// <remarks>
/// These functions provide endpoints for saving, retrieving, and managing
/// shared analyses. The data is stored in Azure Blob Storage and Azure Table Storage.
/// </remarks>
public class SharingFunctions
{
    private const string ContainerName = "shared-analyses";
    private const string TableName = "SharedAnalyses";
    private readonly ILogger<SharingFunctions> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly TableClient _tableClient;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _sharedAnalysisCache = new();

    /// <summary>
    /// Initializes a new instance of the SharingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="authMiddleware">Authentication middleware for request validation</param>
    /// <param name="configuration">Configuration for connection strings</param>
    public SharingFunctions(ILogger<SharingFunctions> logger, AuthenticationMiddleware authMiddleware, IConfiguration configuration)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        
        // Initialize table client
        var storageConnection = configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
        var tableServiceClient = new TableServiceClient(storageConnection);
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    /// <summary>
    /// Saves an analysis to Azure Blob Storage for sharing
    /// </summary>
    /// <param name="req">HTTP request containing the analysis data</param>
    /// <param name="containerClient">Azure Blob container client injected by the runtime</param>
    /// <returns>HTTP response with the unique ID for accessing the shared analysis</returns>
    /// <remarks>
    /// The request body should contain a JSON representation of an AnalysisData object.
    /// The function generates a unique ID and stores the analysis in Azure Blob Storage.
    /// The ID is returned and can be used to retrieve the analysis later.
    /// </remarks>
    /// <example>
    /// POST /api/SaveSharedAnalysis
    /// Content-Type: application/json
    /// 
    /// {
    ///   "title": "Analysis of GitHub Feedback",
    ///   "content": "## Analysis Results\n\nKey findings...",
    ///   "source": "GitHub",
    ///   "createdAt": "2023-06-15T10:30:00Z"
    /// }
    /// </example>
    [Function("SaveSharedAnalysis")]
    [Authorize]
    public async Task<HttpResponseData> SaveSharedAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
        [BlobInput(ContainerName, Connection = "ProductionStorage")] BlobContainerClient containerClient)
    {
        _logger.LogInformation("Processing shared analysis save request");
        
        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("User authentication failed");
            return unauthorizedResponse;
        }
        
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        // Parse request to get both analysis data and public flag
        var requestData = JsonSerializer.Deserialize<SaveAnalysisRequest>(requestBody, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (requestData?.Analysis == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid analysis data");
            return badResponse;
        }

        var analysisData = requestData.Analysis;
        var isPublic = requestData.IsPublic;
        
        // Generate unique ID
        string id = Guid.NewGuid().ToString();
        
        try
        {
            // Save to blob storage with the ID as the blob name
            var blobClient = containerClient.GetBlobClient($"{id}.json");
            
            // Store the original analysis data (not the request wrapper)
            var analysisJson = JsonSerializer.Serialize(analysisData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(analysisJson));
            await blobClient.UploadAsync(ms, overwrite: true);

            // Save metadata to table storage with public flag
            var sharedAnalysisEntity = new SharedAnalysisEntity(user.UserId, id, analysisData, isPublic);
            await _tableClient.UpsertEntityAsync(sharedAnalysisEntity);

            // Add to in-memory cache
            _sharedAnalysisCache[id] = analysisJson;

            _logger.LogInformation("Successfully saved shared analysis {Id} for user {UserId} (Public: {IsPublic})", id, user.UserId, isPublic);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { id }));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving shared analysis for user {UserId}", user.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error saving shared analysis");
            return errorResponse;
        }
    }

    [Function("GetSharedAnalysis")]
    public async Task<HttpResponseData> GetSharedAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetSharedAnalysis/{id}")] HttpRequestData req,
        string id,
        [BlobInput($"{ContainerName}/{{id}}.json", Connection = "ProductionStorage")] string? analysisJson)
    {
        _logger.LogInformation("Retrieving shared analysis with ID: {Id}", id);
        _logger.LogDebug("Blob input analysisJson is {IsNull}", analysisJson == null ? "null" : "not null");

        // First, check if the analysis exists in table storage to get privacy settings
        SharedAnalysisEntity? analysisEntity = null;
        try
        {
            _logger.LogDebug("Querying table storage for analysis with RowKey: {Id}", id);
            // Try to find the entity by scanning all partitions (since we don't know the user ID)
            await foreach (var entity in _tableClient.QueryAsync<SharedAnalysisEntity>(
                filter: $"RowKey eq '{id}'"))
            {
                _logger.LogDebug("Found entity in table storage: UserId={UserId}, IsPublic={IsPublic}",
                    entity.UserId, entity.IsPublic);
                analysisEntity = entity;
                break; // Found the entity
            }

            if (analysisEntity == null)
            {
                _logger.LogWarning("No entity found in table storage for analysis ID: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying table storage for analysis {Id}", id);
        }

        if (analysisEntity == null)
        {
            _logger.LogWarning("Analysis entity not found for ID: {Id}", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("Shared analysis not found");
            return notFoundResponse;
        }

        _logger.LogDebug("Analysis entity found - UserId: {UserId}, IsPublic: {IsPublic}, CreatedDate: {CreatedDate}",
            analysisEntity.UserId, analysisEntity.IsPublic, analysisEntity.CreatedDate);

        // Check access permissions
        bool userOwnsAnalysis = false;
        string? authenticatedUserId = null;
        if (req.Headers.Contains("X-MS-CLIENT-PRINCIPAL"))
        {
            _logger.LogInformation("X-MS-CLIENT-PRINCIPAL header found, attempting to authenticate user");
            try
            {
                var (user, _) = await req.AuthenticateAsync(_authMiddleware);
                if (user != null)
                {
                    authenticatedUserId = user.UserId;
                    userOwnsAnalysis = user.UserId == analysisEntity.UserId;
                    _logger.LogDebug("User authenticated - UserId: {UserId}, OwnsAnalysis: {OwnsAnalysis}",
                        user.UserId, userOwnsAnalysis);
                }
                else
                {
                    _logger.LogDebug("Authentication succeeded but user is null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Authentication failed for analysis access");
            }
        }
        else
        {
            _logger.LogInformation("No X-MS-CLIENT-PRINCIPAL header found in request");
        }

        // If analysis is private and user doesn't own it, deny access
        if (!analysisEntity.IsPublic && !userOwnsAnalysis)
        {
            _logger.LogWarning("Access denied for analysis {Id} - IsPublic: {IsPublic}, UserOwnsAnalysis: {UserOwnsAnalysis}, AuthenticatedUserId: {AuthenticatedUserId}, OwnerUserId: {OwnerUserId}",
                id, analysisEntity.IsPublic, userOwnsAnalysis, authenticatedUserId, analysisEntity.UserId);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("This analysis is private and you don't have permission to access it");
            return unauthorizedResponse;
        }

        _logger.LogDebug("Access granted for analysis {Id}", id);

        // Check in-memory cache first
        if (_sharedAnalysisCache.TryGetValue(id, out var cachedJson))
        {
            var cachedResponse = req.CreateResponse(HttpStatusCode.OK);
            cachedResponse.Headers.Add("Content-Type", "application/json");
            await cachedResponse.WriteStringAsync(cachedJson);
            return cachedResponse;
        }

        if (string.IsNullOrEmpty(analysisJson))
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("Shared analysis content not found");
            return notFoundResponse;
        }

        // Add to cache for future requests
        _sharedAnalysisCache[id] = analysisJson;

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(analysisJson);

        return response;
    }

    /// <summary>
    /// Updates the visibility (public/private) of a shared analysis
    /// </summary>
    /// <param name="req">HTTP request containing the new visibility setting</param>
    /// <param name="id">The ID of the analysis to update</param>
    /// <returns>HTTP response indicating success or failure</returns>
    [Function("UpdateAnalysisVisibility")]
    [Authorize]
    public async Task<HttpResponseData> UpdateAnalysisVisibility(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "UpdateAnalysisVisibility/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Processing request to update analysis visibility for {Id}", id);

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("User authentication failed");
            return unauthorizedResponse;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Analysis ID is required");
            return badRequestResponse;
        }

        try
        {
            // Parse request body to get new visibility setting
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var visibilityRequest = JsonSerializer.Deserialize<UpdateVisibilityRequest>(requestBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (visibilityRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Check if the analysis exists and belongs to the user
            var existingEntity = await _tableClient.GetEntityIfExistsAsync<SharedAnalysisEntity>(user.UserId, id);
            
            if (!existingEntity.HasValue)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Analysis not found or you don't have permission to modify it");
                return notFoundResponse;
            }

            var entity = existingEntity.Value;
            if (entity == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Analysis entity is null");
                return notFoundResponse;
            }

            var isPublic = visibilityRequest.IsPublic;
            
            // Update visibility settings
            entity.IsPublic = isPublic;
            entity.PublicSharedDate = isPublic ? DateTime.UtcNow : null;

            // Save updated entity
            await _tableClient.UpsertEntityAsync(entity);

            _logger.LogInformation("Successfully updated analysis {Id} visibility to {IsPublic} for user {UserId}", 
                id, isPublic, user.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Analysis visibility updated successfully");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating analysis visibility for {Id} and user {UserId}", id, user.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error updating analysis visibility");
            return errorResponse;
        }
    }

    /// <summary>
    /// Timer-triggered function to clean up old shared analyses
    /// </summary>
    /// <param name="timerInfo">Timer information</param>
    /// <param name="containerClient">Azure Blob container client injected by the runtime</param>
    [Function("CleanupOldAnalyses")]
    public async Task CleanupOldAnalyses(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo,  // Run at midnight every day
        [BlobInput(ContainerName, Connection = "ProductionStorage")] BlobContainerClient containerClient)
    {
        _logger.LogInformation($"Starting cleanup of old analyses at: {DateTime.UtcNow}");

        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var deletedBlobCount = 0;
        var deletedTableCount = 0;

        // Clean up old blobs
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            if (blobItem.Properties.LastModified <= cutoffDate)
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync();

                // Remove from cache if present
                var id = Path.GetFileNameWithoutExtension(blobItem.Name);
                _sharedAnalysisCache.TryRemove(id, out _);

                deletedBlobCount++;
            }
        }

        // Clean up old table entries
        var cutoffDateTime = DateTime.UtcNow.AddDays(-30);
        await foreach (var entity in _tableClient.QueryAsync<SharedAnalysisEntity>())
        {
            if (entity.CreatedDate <= cutoffDateTime)
            {
                await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                deletedTableCount++;
            }
        }

        _logger.LogInformation($"Cleanup completed. Deleted {deletedBlobCount} blob(s) and {deletedTableCount} table record(s).");
    }

    /// <summary>
    /// Gets all saved analyses for the authenticated user
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with the user's saved analyses</returns>
    [Function("GetUsersSavedAnalysis")]
    [Authorize]
    public async Task<HttpResponseData> GetUsersSavedAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequestData req)
    {
        _logger.LogInformation("Processing request to get user's saved analyses");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("User authentication failed");
            return unauthorizedResponse;
        }

        try
        {
            var savedAnalyses = new List<SharedAnalysisEntity>();

            // Query table storage for all analyses owned by this user
            await foreach (var entity in _tableClient.QueryAsync<SharedAnalysisEntity>(
                filter: $"PartitionKey eq '{user.UserId}'"))
            {
                savedAnalyses.Add(entity);
            }

            // Sort by creation date (newest first)
            savedAnalyses = savedAnalyses.OrderByDescending(a => a.CreatedDate).ToList();

            _logger.LogInformation("Found {Count} saved analyses for user {UserId}", savedAnalyses.Count, user.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(savedAnalyses, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved analyses for user {UserId}", user.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrieving saved analyses");
            return errorResponse;
        }
    }

    /// <summary>
    /// Deletes a specific shared analysis if the user is authenticated and owns it
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="id">The ID of the analysis to delete</param>
    /// <param name="containerClient">Azure Blob container client injected by the runtime</param>
    /// <returns>HTTP response indicating success or failure</returns>
    [Function("DeleteSharedAnalysis")]
    [Authorize]
    public async Task<HttpResponseData> DeleteSharedAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "DeleteSharedAnalysis/{id}")] HttpRequestData req,
        string id,
        [BlobInput(ContainerName, Connection = "ProductionStorage")] BlobContainerClient containerClient)
    {
        _logger.LogInformation("Processing request to delete shared analysis {Id}", id);

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        if (user == null)
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("User authentication failed");
            return unauthorizedResponse;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Analysis ID is required");
            return badRequestResponse;
        }

        try
        {
            // First, check if the analysis exists and belongs to the user
            var existingEntity = await _tableClient.GetEntityIfExistsAsync<SharedAnalysisEntity>(user.UserId, id);
            
            if (!existingEntity.HasValue)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Analysis not found or you don't have permission to delete it");
                return notFoundResponse;
            }

            // Delete from table storage
            await _tableClient.DeleteEntityAsync(user.UserId, id);

            // Delete from blob storage
            var blobClient = containerClient.GetBlobClient($"{id}.json");
            await blobClient.DeleteIfExistsAsync();

            // Remove from in-memory cache if present
            _sharedAnalysisCache.TryRemove(id, out _);

            _logger.LogInformation("Successfully deleted shared analysis {Id} for user {UserId}", id, user.UserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Analysis deleted successfully");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shared analysis {Id} for user {UserId}", id, user.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error deleting shared analysis");
            return errorResponse;
        }
    }
}