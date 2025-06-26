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
/// Azure Functions for automated report processing
/// </summary>
public class ReportProcessorFunctions
{
    private readonly ILogger<ReportProcessorFunctions> _logger;
    private readonly IConfiguration _configuration;
    private const string TableName = "reportrequests";
    private readonly TableClient _tableClient;
    private readonly BlobServiceClient _serviceClient;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ReportGenerator _reportGenerator;

    public ReportProcessorFunctions(
        ILogger<ReportProcessorFunctions> logger,
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

        // Initialize blob service client
        _serviceClient = new BlobServiceClient(storageConnection);

        // Initialize report generator
        var reportsContainerClient = _serviceClient.GetBlobContainerClient("reports");
        reportsContainerClient.CreateIfNotExists();
        _reportGenerator = new ReportGenerator(_logger, redditService, githubService, analyzerService, reportsContainerClient);
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

            var generatedReports = new List<ReportModel>();
            var failedRequests = new List<string>();

            foreach (var request in requests)
            {
                var report = await _reportGenerator.ProcessReportRequestAsync(request);
                if (report != null)
                {
                    generatedReports.Add(report);
                    _logger.LogInformation("Successfully processed request {RequestId} and generated report {ReportId}. Report automatically stored in 'reports' blob container.", 
                        request.Id, report.Id);
                }
                else
                {
                    failedRequests.Add(request.Id);
                    _logger.LogWarning("Failed to process request {RequestId}", request.Id);
                }
            }

            // Store summary of the weekly processing session
            await StoreWeeklyProcessingSummaryAsync(generatedReports, failedRequests);

            _logger.LogInformation("Completed weekly report processing. Generated {SuccessCount} reports, {FailureCount} failures", 
                generatedReports.Count, failedRequests.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in weekly report processing");
        }
    }

    /// <summary>
    /// Get weekly processing summaries for monitoring
    /// </summary>
    [Function("GetWeeklyProcessingSummaries")]
    public async Task<HttpResponseData> GetWeeklyProcessingSummaries(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Retrieving weekly processing summaries");

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var limitParam = queryParams["limit"];
            var limit = int.TryParse(limitParam, out var parsedLimit) ? parsedLimit : 10;

            var summaryContainerClient = _serviceClient.GetBlobContainerClient("weekly-summaries");
            var summaries = new List<object>();

            await foreach (var blob in summaryContainerClient.GetBlobsAsync())
            {
                if (summaries.Count >= limit) break;

                var blobClient = summaryContainerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var summaryJson = content.Value.Content.ToString();
                var summary = JsonSerializer.Deserialize<object>(summaryJson, _jsonOptions);
                
                summaries.Add(new
                {
                    fileName = blob.Name,
                    lastModified = blob.Properties.LastModified,
                    summary = summary
                });
            }

            // Sort by last modified descending
            summaries = summaries.OrderByDescending(s => ((dynamic)s).lastModified).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { summaries }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weekly processing summaries");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving summaries: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Stores a summary of the weekly processing session
    /// </summary>
    /// <param name="generatedReports">List of successfully generated reports</param>
    /// <param name="failedRequests">List of failed request IDs</param>
    private async Task StoreWeeklyProcessingSummaryAsync(List<ReportModel> generatedReports, List<string> failedRequests)
    {
        try
        {
            var summary = new
            {
                ProcessedAt = DateTime.UtcNow,
                TotalRequests = generatedReports.Count + failedRequests.Count,
                SuccessfulReports = generatedReports.Count,
                FailedRequests = failedRequests.Count,
                GeneratedReports = generatedReports.Select(r => new
                {
                    r.Id,
                    r.Source,
                    r.SubSource,
                    r.GeneratedAt,
                    r.ThreadCount,
                    r.CommentCount
                }).ToList(),
                FailedRequestIds = failedRequests
            };

            var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            var summaryFileName = $"weekly-summary-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json";
            
            var summaryContainerClient = _serviceClient.GetBlobContainerClient("weekly-summaries");
            await summaryContainerClient.CreateIfNotExistsAsync();
            
            var summaryBlobClient = summaryContainerClient.GetBlobClient(summaryFileName);
            await using var summaryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(summaryJson));
            await summaryBlobClient.UploadAsync(summaryStream, overwrite: true);

            _logger.LogInformation("Stored weekly processing summary as {SummaryFileName}", summaryFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing weekly processing summary");
        }
    }
}
