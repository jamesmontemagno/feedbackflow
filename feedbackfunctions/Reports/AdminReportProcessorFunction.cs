using Microsoft.Azure.Functions.Worker;
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

    public AdminReportProcessorFunction(
        ILogger<AdminReportProcessorFunction> logger,
        IAdminReportConfigService adminReportConfigService,
        IReportCacheService cacheService,
        IEmailService emailService,
        IRedditService redditService,
        IGitHubService githubService,
        IFeedbackAnalyzerService analyzerService,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _logger = logger;
        _adminReportConfigService = adminReportConfigService;
        _cacheService = cacheService;
        _emailService = emailService;
        
        // Initialize ReportGenerator with required dependencies
        var storageConnection = configuration["ProductionStorage"] ?? 
                              throw new InvalidOperationException("ProductionStorage connection string not found");
        var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConnection);
        
        _reportGenerator = new ReportGenerator(logger, redditService, githubService, analyzerService, 
                                             blobServiceClient, configuration, cacheService);
    }

    /// <summary>
    /// Process all active admin report configurations
    /// Runs every Tuesday at 2:00 AM UTC (1 hour after weekly reports at 1:00 AM)
    /// </summary>
    [Function("ProcessAdminReports")]
    public async Task ProcessAdminReportsAsync([TimerTrigger("0 0 2 * * 2")] TimerInfo timer)
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
                    
                    report = await _reportGenerator.GenerateRedditReportAsync(config.Subreddit);
                }
                else if (config.Type.Equals("github", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(config.Owner) || string.IsNullOrEmpty(config.Repo))
                    {
                        throw new InvalidOperationException($"Owner and Repo are required for GitHub report config {config.Id}");
                    }
                    
                    report = await _reportGenerator.GenerateGitHubReportAsync(config.Owner, config.Repo);
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
            var emailModel = new WeeklyReportEmailModel
            {
                RecipientEmail = config.EmailRecipient,
                RecipientName = "Administrator", // Generic name for admin emails
                ReportTitle = config.Name,
                ReportData = report,
                WeekStartDate = DateTime.UtcNow.AddDays(-7), // Last week
                WeekEndDate = DateTime.UtcNow,
                IsAdminReport = true // Flag to indicate this is an admin report
            };

            await _emailService.SendWeeklyReportEmailAsync(emailModel);
            
            _logger.LogInformation("Admin report email sent successfully for config {ConfigId} to {Email}", 
                config.Id, config.EmailRecipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending admin report email for config {ConfigId} to {Email}", 
                config.Id, config.EmailRecipient);
            throw;
        }
    }
}