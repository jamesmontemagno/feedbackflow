using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Utils;
using FeedbackFunctions.Services;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Services.Email;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Models.Email;
using SharedDump.Models.Account;
using SharedDump.Models.Reports;
using SharedDump.Utils.Account;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for automated report processing
/// </summary>
public class ReportProcessorFunctions
{
    private readonly ILogger<ReportProcessorFunctions> _logger;
    private readonly IReportStorageService _reportStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ReportGenerator _reportGenerator;
    private readonly IReportCacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly IUserAccountService _userAccountService;

    public ReportProcessorFunctions(
        ILogger<ReportProcessorFunctions> logger,
        IReportStorageService reportStorage,
        ReportGenerator reportGenerator,
        IReportCacheService cacheService,
        IEmailService emailService,
        IUserAccountService userAccountService)
    {
        _logger = logger;
        _reportStorage = reportStorage;
        _reportGenerator = reportGenerator;
        _cacheService = cacheService;
        _emailService = emailService;
        _userAccountService = userAccountService;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting Reddit Report processing for URL {RequestUrl}", req.Url);

        // Validate API key and check usage limits (Reports = 2 usage points)
        var apiKeyService = req.FunctionContext.InstanceServices.GetRequiredService<IApiKeyService>();
        var userAccountService = req.FunctionContext.InstanceServices.GetRequiredService<IUserAccountService>();
        var (isValid, errorResponse, userId) = await ApiKeyValidationHelper.ValidateApiKeyWithUsageAsync(req, apiKeyService, userAccountService, _logger, 2);
        if (!isValid)
            return errorResponse!;

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

        // Normalize to lowercase for consistent processing
        subreddit = subreddit.ToLowerInvariant();

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
            var reportResolutionElapsedMs = stopwatch.ElapsedMilliseconds;

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Report processing completed for r/{Subreddit} in {ProcessingTime:c}", 
                subreddit, processingTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(report.HtmlContent ?? string.Empty);
            var responseWriteElapsedMs = stopwatch.ElapsedMilliseconds;
            
            // Track API usage on successful completion (Reports = 2 usage points)
            await ApiKeyValidationHelper.TrackApiUsageAsync(userId!, 2, userAccountService, _logger, $"reddit:{subreddit}");
            var usageTrackingElapsedMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "RedditReport performance: totalMs={TotalMs}, reportResolutionMs={ReportResolutionMs}, responseWriteMs={ResponseWriteMs}, usageTrackingMs={UsageTrackingMs}, force={Force}, usedCachedReport={UsedCachedReport}, htmlLength={HtmlLength}",
                usageTrackingElapsedMs,
                reportResolutionElapsedMs,
                responseWriteElapsedMs - reportResolutionElapsedMs,
                usageTrackingElapsedMs - responseWriteElapsedMs,
                force,
                recentReport is not null,
                report.HtmlContent?.Length ?? 0);
            
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting GitHub Issues Report processing for URL {RequestUrl}", req.Url);

        // Validate API key and check usage limits (Reports = 2 usage points)
        var apiKeyService = req.FunctionContext.InstanceServices.GetRequiredService<IApiKeyService>();
        var userAccountService = req.FunctionContext.InstanceServices.GetRequiredService<IUserAccountService>();
        var (isValid, errorResponse, userId) = await ApiKeyValidationHelper.ValidateApiKeyWithUsageAsync(req, apiKeyService, userAccountService, _logger, 2);
        if (!isValid)
            return errorResponse!;

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

        // Normalize to lowercase for consistent processing
        var repoOwner = repoParts[0].ToLowerInvariant();
        var repoName = repoParts[1].ToLowerInvariant();
        var normalizedRepo = $"{repoOwner}/{repoName}";

        try
        {
            // First, check if we have a recent report (last 24 hours) unless forced
            var recentReport = force ? null : await GetRecentReportAsync("github", normalizedRepo);
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
            var reportResolutionElapsedMs = stopwatch.ElapsedMilliseconds;

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("GitHub Issues Report processing completed for {RepoOwner}/{RepoName} in {ProcessingTime:c}", 
                repoOwner, repoName, processingTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(report.HtmlContent ?? string.Empty);
            var responseWriteElapsedMs = stopwatch.ElapsedMilliseconds;
            
            // Track API usage on successful completion (Reports = 2 usage points)
            await ApiKeyValidationHelper.TrackApiUsageAsync(userId!, 2, userAccountService, _logger, $"github:{normalizedRepo}");
            var usageTrackingElapsedMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "GitHubIssuesReport performance: totalMs={TotalMs}, reportResolutionMs={ReportResolutionMs}, responseWriteMs={ResponseWriteMs}, usageTrackingMs={UsageTrackingMs}, force={Force}, usedCachedReport={UsedCachedReport}, days={Days}, htmlLength={HtmlLength}",
                usageTrackingElapsedMs,
                reportResolutionElapsedMs,
                responseWriteElapsedMs - reportResolutionElapsedMs,
                usageTrackingElapsedMs - responseWriteElapsedMs,
                force,
                recentReport is not null,
                days,
                report.HtmlContent?.Length ?? 0);
            
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
    /// Generates a summary version of a Reddit report with stats, top posts, and weekly summary only
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a summary HTML-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - subreddit: Required. The name of the subreddit to analyze
    /// - force: Optional. If true, bypasses cache and forces new report generation (default: false)
    /// 
    /// The function fetches a recent summary report (within last 24 hours) or generates a new one.
    /// Summary reports contain only essential information: statistics, top posts, and weekly summary.
    /// </remarks>
    /// <example>
    /// GET /api/RedditReportSummary?subreddit=dotnet&amp;force=true
    /// </example>
    [Function("RedditReportSummary")]
    public async Task<HttpResponseData> RedditReportSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting Reddit Summary Report processing for URL {RequestUrl}", req.Url);

        // Validate API key and check usage limits (Reports = 2 usage points)
        var apiKeyService = req.FunctionContext.InstanceServices.GetRequiredService<IApiKeyService>();
        var userAccountService = req.FunctionContext.InstanceServices.GetRequiredService<IUserAccountService>();
        var (isValid, errorResponse, userId) = await ApiKeyValidationHelper.ValidateApiKeyWithUsageAsync(req, apiKeyService, userAccountService, _logger, 2);
        if (!isValid)
            return errorResponse!;

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var subreddit = queryParams["subreddit"];
        var forceParam = queryParams["force"];
        var force = bool.TryParse(forceParam, out var parsedForce) && parsedForce;

        if (string.IsNullOrEmpty(subreddit))
        {
            _logger.LogWarning("Reddit Summary Report request rejected - missing subreddit parameter");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Subreddit parameter is required");
            return response;
        }

        // Normalize to lowercase for consistent processing
        subreddit = subreddit.ToLowerInvariant();

        try
        {
            // First, check if we have a recent summary report (last 24 hours) unless forced
            var recentSummaryReport = force ? null : await _reportGenerator.GetRecentSummaryReportAsync("reddit", subreddit);
            ReportModel summaryReport;

            if (recentSummaryReport != null)
            {
                _logger.LogInformation("Using existing summary report {ReportId} for r/{Subreddit} generated {TimeSinceGeneration} ago", 
                    recentSummaryReport.Id, subreddit, DateTime.UtcNow - recentSummaryReport.GeneratedAt);
                summaryReport = recentSummaryReport;
            }
            else
            {
                var logMessage = force 
                    ? "Force parameter specified, generating new summary report for r/{Subreddit}" 
                    : "No recent summary report found for r/{Subreddit}, generating new summary report";
                _logger.LogInformation(logMessage, subreddit);
                
                // Generate a full report which will automatically create a summary report
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
                var fullReport = await _reportGenerator.GenerateRedditReportAsync(subreddit, cutoffDate, storeToBlob: true);
                
                // Get the summary report that was just created
                summaryReport = await _reportGenerator.GetRecentSummaryReportAsync("reddit", subreddit) ?? 
                    throw new InvalidOperationException("Summary report was not created as expected");
            }

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Summary Report processing completed for r/{Subreddit} in {ProcessingTime:c}", 
                subreddit, processingTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(summaryReport.HtmlContent ?? string.Empty);
            
            // Track API usage on successful completion (Reports = 2 usage points)
            await ApiKeyValidationHelper.TrackApiUsageAsync(userId!, 2, userAccountService, _logger, $"reddit-summary:{subreddit}");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit summary report for r/{Subreddit}. Processing time: {ProcessingTime:c}", 
                subreddit, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing summary report: {ex.Message}");
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
            
            await _reportStorage.EnsureInitializedAsync();

            await foreach (var entity in _reportStorage.ReportRequestsTable.QueryAsync<ReportRequestModel>())
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

                // Wait 45 seconds between reports to avoid rate limiting
                if (request != requests.Last())
                {
                    _logger.LogInformation("Waiting 45 seconds before processing next report to avoid rate limiting...");
                    await Task.Delay(TimeSpan.FromSeconds(45));
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

            await _reportStorage.EnsureInitializedAsync();
            var summaryContainerClient = _reportStorage.WeeklySummariesContainer;
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
            
            await _reportStorage.EnsureInitializedAsync();
            var summaryBlobClient = _reportStorage.WeeklySummariesContainer.GetBlobClient(summaryFileName);
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
