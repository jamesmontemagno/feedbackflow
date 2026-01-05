using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Services.Email;
using SharedDump.Services.Interfaces;
using SharedDump.AI;
using FeedbackFunctions.Services;
using FeedbackFunctions.Utils;
using SharedDump.Models.Reports;
using FeedbackFunctions.Models.Email;
using Azure.Storage.Blobs;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using Microsoft.Extensions.Configuration;

namespace FeedbackFunctions.Reports;

/// <summary>
/// Azure Function to process admin report configurations and send emails
/// Runs 1 hour after the weekly report processor (Tuesday 2:00 AM UTC)
/// </summary>
public class AdminReportProcessorFunction
{
    private readonly ILogger<AdminReportProcessorFunction> _logger;
    private readonly IAdminReportConfigService _adminReportConfigService;
    private readonly IReportCacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ReportGenerator _reportGenerator;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;
    private readonly IConfiguration _configuration;

    public AdminReportProcessorFunction(
        ILogger<AdminReportProcessorFunction> logger,
        IAdminReportConfigService adminReportConfigService,
        IReportCacheService cacheService,
        IEmailService emailService,
        IRedditService redditService,
        IGitHubService githubService,
    IFeedbackAnalyzerService analyzerService,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    AuthenticationMiddleware authMiddleware,
    IUserAccountService userAccountService)
    {
        _logger = logger;
        _adminReportConfigService = adminReportConfigService;
        _cacheService = cacheService;
        _emailService = emailService;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
        _configuration = configuration;
        
        // Initialize ReportGenerator with required dependencies
        var storageConnection = configuration["ProductionStorage"] ?? 
                              throw new InvalidOperationException("ProductionStorage connection string not found");
        var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConnection);
        
        _reportGenerator = new ReportGenerator(logger, redditService, githubService, analyzerService, 
                                             blobServiceClient, configuration, cacheService);
    }

    /// <summary>
    /// Process all active admin report configurations
    /// Runs every Monday at 3:00 PM UTC (after reports are generated at 11:00 AM)
    /// </summary>
    [Function("ProcessAdminReports")]
    public async Task ProcessAdminReportsAsync([TimerTrigger("0 0 15 * * 1")] TimerInfo timer)
    {
        _logger.LogInformation("ProcessAdminReports function triggered at {TriggerTime}", DateTime.UtcNow);

        try
        {
            // Get all active admin report configurations
            var activeConfigs = await _adminReportConfigService.GetAllActiveConfigsAsync();
            
            if (!activeConfigs.Any())
            {
                _logger.LogInformation("No active admin report configurations found");
                return;
            }

            _logger.LogInformation("Processing {Count} active admin report configurations", activeConfigs.Count);

            var processedCount = 0;
            var failedCount = 0;

            // Process each configuration
            foreach (var config in activeConfigs)
            {
                try
                {
                    await ProcessSingleAdminReportAsync(config);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing admin report config {ConfigId} '{ConfigName}'", 
                        config.Id, config.Name);
                    failedCount++;
                }

                // Wait 45 seconds between reports to avoid rate limiting
                if (config != activeConfigs.Last())
                {
                    _logger.LogInformation("Waiting 45 seconds before processing next admin report to avoid rate limiting...");
                    await Task.Delay(TimeSpan.FromSeconds(45));
                }
            }

            _logger.LogInformation("Admin report processing completed. Processed: {ProcessedCount}, Failed: {FailedCount}", 
                processedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessAdminReports function");
        }
    }

    /// <summary>
    /// HTTP-trigger to process and send a single admin report immediately by configuration ID
    /// </summary>
    [Function("SendAdminReportNow")]
    [Authorize]
    public async Task<HttpResponseData> SendAdminReportNow(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            // Authenticate and ensure admin access
            var authenticatedUser = await _authMiddleware.GetUserAsync(req);
            if (authenticatedUser is null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Authentication required");
                return unauthorized;
            }

            var userAccount = await _userAccountService.GetUserAccountAsync(authenticatedUser.UserId);
            if (userAccount?.Tier != AccountTier.Admin)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("Admin access required");
                return forbidden;
            }

            // Parse query string for id
            var query = QueryHelpers.ParseQuery(req.Url.Query);
            if (!query.TryGetValue("id", out var idValues))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing required query parameter 'id'.");
                return bad;
            }

            var id = idValues.ToString();
            if (string.IsNullOrWhiteSpace(id))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid 'id' value.");
                return bad;
            }

            _logger.LogInformation("SendAdminReportNow invoked for config {ConfigId}", id);

            var config = await _adminReportConfigService.GetConfigAsync(id);
            if (config is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Admin report configuration not found.");
                return notFound;
            }

            await ProcessSingleAdminReportAsync(config);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(new { success = true, id });
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendAdminReportNow endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to send admin report. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Process a single admin report configuration
    /// </summary>
    /// <param name="config">The admin report configuration to process</param>
    private async Task ProcessSingleAdminReportAsync(AdminReportConfigModel config)
    {
        _logger.LogInformation("Processing admin report config {ConfigId} '{ConfigName}' for {Email}", 
            config.Id, config.Name, config.EmailRecipient);

        try
        {
            // Generate cache key based on report type and parameters
            var cacheKey = GenerateCacheKey(config);
            
            // Check if report already exists in cache
            var existingReport = await _cacheService.GetReportAsync(cacheKey);
            
            ReportModel report;
            
            if (existingReport != null)
            {
                _logger.LogInformation("Using existing cached report for config {ConfigId}", config.Id);
                report = existingReport;
            }
            else
            {
                _logger.LogInformation("Generating new report for config {ConfigId}", config.Id);
                
                // Generate the report based on type
                if (config.Type.Equals("reddit", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(config.Subreddit))
                    {
                        throw new InvalidOperationException($"Subreddit is required for Reddit report config {config.Id}");
                    }
                    
                    report = await _reportGenerator.GenerateRedditReportAsync(config.Subreddit, storeToBlob: true);
                }
                else if (config.Type.Equals("github", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(config.Owner) || string.IsNullOrEmpty(config.Repo))
                    {
                        throw new InvalidOperationException($"Owner and Repo are required for GitHub report config {config.Id}");
                    }
                    
                    report = await _reportGenerator.GenerateGitHubReportAsync(config.Owner, config.Repo, storeToBlob: true);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported report type '{config.Type}' for config {config.Id}");
                }

                // Cache the generated report
                await _cacheService.SetReportAsync(report);
                _logger.LogInformation("Cached new report for config {ConfigId} with key {CacheKey}", config.Id, cacheKey);
            }

            // Send the report via email
            await SendAdminReportEmailAsync(config, report);

            // Mark configuration as processed
            await _adminReportConfigService.MarkConfigProcessedAsync(config.Id, DateTimeOffset.UtcNow);

            _logger.LogInformation("Successfully processed admin report config {ConfigId} '{ConfigName}'", 
                config.Id, config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing admin report config {ConfigId} '{ConfigName}'", 
                config.Id, config.Name);
            throw;
        }
    }

    /// <summary>
    /// Generate a cache key for the admin report configuration
    /// </summary>
    /// <param name="config">The admin report configuration</param>
    /// <returns>A cache key string</returns>
    private static string GenerateCacheKey(AdminReportConfigModel config)
    {
        if (config.Type.Equals("reddit", StringComparison.OrdinalIgnoreCase))
        {
            return $"reddit_{config.Subreddit?.ToLowerInvariant()}";
        }
        else if (config.Type.Equals("github", StringComparison.OrdinalIgnoreCase))
        {
            return $"github_{config.Owner?.ToLowerInvariant()}_{config.Repo?.ToLowerInvariant()}";
        }
        else
        {
            throw new InvalidOperationException($"Unsupported report type: {config.Type}");
        }
    }

    /// <summary>
    /// Send an admin report via email
    /// </summary>
    /// <param name="config">The admin report configuration</param>
    /// <param name="report">The generated report</param>
    private async Task SendAdminReportEmailAsync(AdminReportConfigModel config, ReportModel report)
    {
        try
        {

            var emailRequest = new ReportEmailRequest
            {
                RecipientEmail = config.EmailRecipient,
                RecipientName = config.Name, // Could enhance with actual user name
                ReportId = report.Id.ToString(),
                ReportTitle = $"{report.Source} Report - {report.SubSource}",
                ReportSummary = $"Your {report.Source} report has been generated",
                ReportUrl = WebUrlHelper.BuildReportUrl(_configuration, report.Id), // Adjust URL as needed
                ReportType = report.Source,
                GeneratedAt = report.GeneratedAt.DateTime,
                HtmlContent = report.HtmlContent
            };

            var deliveryStatus = await _emailService.SendReportEmailAsync(emailRequest);

            if (deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Succeeded || 
                deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Running)
            {
                _logger.LogInformation("Successfully sent individual report email to {Email} for report {ReportId}", 
                    config.EmailRecipient, report.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send individual report email to {Email} for report {ReportId}: {Error}", 
                    config.EmailRecipient, report.Id, deliveryStatus.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending admin report email for config {ConfigId} to {Email}", 
                config.Id, config.EmailRecipient);
            throw;
        }
    }
}