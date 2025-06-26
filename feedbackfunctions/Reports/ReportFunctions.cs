using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reddit;
using SharedDump.Models.GitHub;
using SharedDump.Models.Reports;
using SharedDump.AI;
using SharedDump.Services.Interfaces;
using SharedDump.Utils;
using Azure.Storage.Blobs;
using FeedbackFunctions.Utils;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for generating reports based on feedback data
/// </summary>
/// <remarks>
/// This class contains functions that generate comprehensive reports
/// by analyzing feedback from various platforms. The reports are typically
/// formatted in markdown and may include sentiment analysis, trends, and key insights.
/// </remarks>
public class ReportingFunctions
{
    private readonly ILogger<ReportingFunctions> _logger;
    private readonly IRedditService _redditService;
    private readonly IGitHubService _githubService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;
    private const string ContainerName = "reports";
    private readonly BlobContainerClient _containerClient;
    private readonly ReportGenerator _reportGenerator;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the ReportingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="redditService">Reddit service for subreddit operations</param>
    /// <param name="githubService">GitHub service for repository operations</param>
    /// <param name="analyzerService">Feedback analyzer service for AI-powered analysis</param>
    public ReportingFunctions(
        ILogger<ReportingFunctions> logger,
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
        _redditService = redditService;
        _githubService = githubService;
        _analyzerService = analyzerService;
        
        // Initialize blob container
        var storageConnection = _configuration["AzureWebJobsStorage"] ?? throw new InvalidOperationException("Storage connection string not configured");
        var serviceClient = new BlobServiceClient(storageConnection);
        _containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        _containerClient.CreateIfNotExists();

        // Initialize report generator
        _reportGenerator = new ReportGenerator(_logger, _redditService, _githubService, _analyzerService, _containerClient);
    }

  

    /// <summary>
    /// Gets a report by its ID
    /// </summary>
    /// <param name="req">HTTP request containing the report ID</param>
    /// <returns>HTTP response with the full report data</returns>
    [Function("GetReport")]
    public async Task<HttpResponseData> GetReport(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Report/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("Getting Reddit report with ID: {Id}", id);

        try
        {
            var blobClient = _containerClient.GetBlobClient($"{id}.json");
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Report with ID {Id} not found", id);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Report with ID {id} not found");
                return notFoundResponse;
            }

            var blobContent = await blobClient.DownloadContentAsync();
            var report = JsonSerializer.Deserialize<ReportModel>(blobContent.Value.Content);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(report));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report with ID {Id}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving report: {ex.Message}");
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
            var matchingReports = new List<object>();

            await foreach (var blob in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
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
    /// Lists all reports with basic metadata
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <returns>HTTP response with a list of report summaries</returns>
    /// <remarks>
    /// Query parameters:
    /// - source: Optional. Filter reports by source (e.g., "reddit")
    /// - subsource: Optional. Filter reports by subsource (e.g., "dotnet")
    /// 
    /// If no parameters are provided, returns all reports (backward compatible).
    /// Parameters can be used individually or in combination.
    /// </remarks>
    [Function("ListReports")]
    public async Task<HttpResponseData> ListReports(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sourceFilter = queryParams["source"];
        var subsourceFilter = queryParams["subsource"];
        
        var filterMessage = sourceFilter == null && subsourceFilter == null 
            ? "all reports" 
            : $"reports (source: {sourceFilter ?? "any"}, subsource: {subsourceFilter ?? "any"})";
            
        _logger.LogInformation("Listing {FilterMessage}", filterMessage);

        try
        {
            var reports = new List<object>();
            await foreach (var blob in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var report = JsonSerializer.Deserialize<ReportModel>(content.Value.Content);

                if (report != null)
                {
                    // Apply filtering if parameters are provided
                    var matchesSource = string.IsNullOrEmpty(sourceFilter) || 
                                       string.Equals(report.Source, sourceFilter, StringComparison.OrdinalIgnoreCase);
                    var matchesSubsource = string.IsNullOrEmpty(subsourceFilter) || 
                                          string.Equals(report.SubSource, subsourceFilter, StringComparison.OrdinalIgnoreCase);
                    
                    if (matchesSource && matchesSubsource)
                    {
                        reports.Add(new
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
            await response.WriteStringAsync(JsonSerializer.Serialize(new { reports }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reports");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error listing reports: {ex.Message}");
            return errorResponse;
        }
    }
}
