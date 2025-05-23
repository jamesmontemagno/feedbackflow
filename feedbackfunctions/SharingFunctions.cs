using System.Net;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Models;
using System.Text;
using System.Text.Json;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for sharing and retrieving analysis results
/// </summary>
/// <remarks>
/// These functions provide endpoints for saving, retrieving, and managing
/// shared analyses. The data is stored in Azure Blob Storage.
/// </remarks>
public class SharingFunctions
{
    private const string ContainerName = "shared-analyses";
    private readonly ILogger<SharingFunctions> _logger;

    /// <summary>
    /// Initializes a new instance of the SharingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    public SharingFunctions(ILogger<SharingFunctions> logger)
    {
        _logger = logger;
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
    public async Task<HttpResponseData> SaveSharedAnalysis(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req,
        [BlobInput(ContainerName, Connection = "AzureWebJobsStorage")] BlobContainerClient containerClient)
    {
        _logger.LogInformation("Processing shared analysis save request");
        
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var analysisData = JsonSerializer.Deserialize<AnalysisData>(requestBody, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (analysisData == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid analysis data");
            return badResponse;
        }
        
        // Generate unique ID
        string id = Guid.NewGuid().ToString();
        
        // Save to blob storage with the ID as the blob name
        var blobClient = containerClient.GetBlobClient($"{id}.json");
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        await blobClient.UploadAsync(ms, overwrite: true);
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { id }));
        
        return response;
    }

    [Function("GetSharedAnalysis")]
    public async Task<HttpResponseData> GetSharedAnalysis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetSharedAnalysis/{id}")] HttpRequestData req,
        string id,
        [BlobInput($"{ContainerName}/{{id}}.json", Connection = "AzureWebJobsStorage")] string? analysisJson)
    {
        _logger.LogInformation($"Retrieving shared analysis with ID: {id}");
        
        if (string.IsNullOrEmpty(analysisJson))
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("Shared analysis not found");
            return notFoundResponse;
        }
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(analysisJson);
        
        return response;
    }
}