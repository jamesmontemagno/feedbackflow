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
    private readonly IReportCacheService _cacheService;

    public ReportProcessorFunctions(
        ILogger<ReportProcessorFunctions> logger,
        IConfiguration configuration,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        IReportCacheService cacheService)
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
        _reportGenerator = new ReportGenerator(_logger, redditService, githubService, analyzerService, reportsContainerClient, _cacheService);
    }

    /// <summary>
    /// Checks if there's a recent report (within last 24 hours) for the given source and subsource.
    /// </summary>
    /// <param name="source">The report source (e.g., "reddit", "github")</param>
    /// <param name="subSource">The report subsource (e.g., subreddit name, repo name)</param>
    /// <returns>The most recent report if found within 24 hours, otherwise null</returns>
    private async Task<ReportModel?> GetRecentReportAsync(string source, string subSource)
    {
        try
        {
            _logger.LogDebug("Checking for recent reports with source '{Source}' and subsource '{SubSource}'", source, subSource);
            
            var reports = await _cacheService.GetReportsAsync(source, subSource);
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            
            var recentReport = reports
                .Where(r => r.GeneratedAt >= cutoff)
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefault();

            if (recentReport != null)
            {
                _logger.LogInformation("Found recent report {ReportId} generated at {GeneratedAt} for {Source}/{SubSource}", 
                    recentReport.Id, recentReport.GeneratedAt, source, subSource);
            }
            else
            {
                _logger.LogDebug("No recent report found for {Source}/{SubSource}", source, subSource);
            }

            return recentReport;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for recent reports for {Source}/{SubSource}. Will proceed with generating new report.", source, subSource);
            return null;
        }
    }

      /// <summary>
    /// Generates a comprehensive report of a subreddit's threads and comments
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a markdown-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - subreddit: Required. The name of the subreddit to analyze
    /// - days: Optional. Number of days of history to analyze (default: 7)
    /// - limit: Optional. Maximum number of threads to analyze (default: 25)
    /// - sort: Optional. Sort method for threads ("hot", "new", "top", etc.)
    /// - force: Optional. If true, bypasses cache and forces new report generation (default: false)
    /// 
    /// The function fetches Reddit threads, analyzes their content using AI,
    /// and returns a markdown-formatted report with insights, trends, and key takeaways.
    /// </remarks>
    /// <example>
    /// GET /api/RedditReport?subreddit=dotnet&amp;days=30&amp;limit=50&amp;sort=top&amp;force=true
    /// </example>
    [Function("RedditReport")]
    public async Task<HttpResponseData> RedditReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting Reddit Report processing for URL {RequestUrl}", req.Url);

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var subreddit = queryParams["subreddit"];
        var forceParam = queryParams["force"];
        var force = bool.TryParse(forceParam, out var parsedForce) && parsedForce;

        if (string.IsNullOrEmpty(subreddit))
        {
            _logger.LogWarning("Reddit Report request rejected - missing subreddit parameter");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Subreddit parameter is required");
            return response;
        }

        try
        {
            // First, check if we have a recent report (last 24 hours) unless forced
            var recentReport = force ? null : await GetRecentReportAsync("reddit", subreddit);
            ReportModel report;

            if (recentReport != null)
            {
                _logger.LogInformation("Using existing report {ReportId} for r/{Subreddit} generated {TimeSinceGeneration} ago", 
                    recentReport.Id, subreddit, DateTime.UtcNow - recentReport.GeneratedAt);
                report = recentReport;
            }
            else
            {
                var logMessage = force 
                    ? "Force parameter specified, generating new report for r/{Subreddit}" 
                    : "No recent report found for r/{Subreddit}, generating new report";
                _logger.LogInformation(logMessage, subreddit);
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
                report = await _reportGenerator.GenerateRedditReportAsync(subreddit, cutoffDate);
            }

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Report processing completed for r/{Subreddit} in {ProcessingTime:c}", 
                subreddit, processingTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(report.HtmlContent ?? string.Empty);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit report for r/{Subreddit}. Processing time: {ProcessingTime:c}", 
                subreddit, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Generates a comprehensive report of GitHub repository issues
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a markdown-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - repo: Required. The repository in format "owner/name" (e.g., "microsoft/vscode")
    /// - days: Optional. Number of days of history to analyze (default: 7)
    /// - force: Optional. If true, bypasses cache and forces new report generation (default: false)
    /// 
    /// The function fetches GitHub issues from the last week, analyzes their content using AI,
    /// and returns an HTML-formatted report with insights, trends, and key takeaways.
    /// </remarks>
    /// <example>
    /// GET /api/GitHubIssuesReport?repo=microsoft/vscode&amp;days=7&amp;force=true
    /// </example>
    [Function("GitHubIssuesReport")]
    public async Task<HttpResponseData> GitHubIssuesReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting GitHub Issues Report processing for URL {RequestUrl}", req.Url);

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var repo = queryParams["repo"];
        var daysParam = queryParams["days"];
        var forceParam = queryParams["force"];
        var days = int.TryParse(daysParam, out var parsedDays) ? parsedDays : 7;
        var force = bool.TryParse(forceParam, out var parsedForce) && parsedForce;

        if (string.IsNullOrEmpty(repo))
        {
            _logger.LogWarning("GitHub Issues Report request rejected - missing repo parameter");
            var missingRepoResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await missingRepoResponse.WriteStringAsync("Repository parameter is required (format: owner/name)");
            return missingRepoResponse;
        }

        var repoParts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (repoParts.Length != 2)
        {
            _logger.LogWarning("GitHub Issues Report request rejected - invalid repo format: {Repo}", repo);
            var invalidFormatResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await invalidFormatResponse.WriteStringAsync("Repository parameter must be in format 'owner/name'");
            return invalidFormatResponse;
        }

        var repoOwner = repoParts[0];
        var repoName = repoParts[1];

        try
        {
            // First, check if we have a recent report (last 24 hours) unless forced
            var recentReport = force ? null : await GetRecentReportAsync("github", repo);
            ReportModel report;

            if (recentReport != null)
            {
                _logger.LogInformation("Using existing report {ReportId} for {RepoOwner}/{RepoName} generated {TimeSinceGeneration} ago", 
                    recentReport.Id, repoOwner, repoName, DateTime.UtcNow - recentReport.GeneratedAt);
                report = recentReport;
            }
            else
            {
                var logMessage = force 
                    ? "Force parameter specified, generating new report for {RepoOwner}/{RepoName}" 
                    : "No recent report found for {RepoOwner}/{RepoName}, generating new report";
                _logger.LogInformation(logMessage, repoOwner, repoName);
                report = await _reportGenerator.GenerateGitHubReportAsync(repoOwner, repoName, days);
            }

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("GitHub Issues Report processing completed for {RepoOwner}/{RepoName} in {ProcessingTime:c}", 
                repoOwner, repoName, processingTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(report.HtmlContent ?? string.Empty);
            return response;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found or not accessible"))
        {
            _logger.LogWarning("Repository {RepoOwner}/{RepoName} not found or not accessible", repoOwner, repoName);
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Repository not found or not accessible");
            return badRequestResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub Issues report for {RepoOwner}/{RepoName}. Processing time: {ProcessingTime:c}", 
                repoOwner, repoName, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
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
