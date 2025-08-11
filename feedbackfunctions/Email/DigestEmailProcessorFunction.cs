using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;
using SharedDump.Models.Account;
using SharedDump.Utils.Account;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using FeedbackFunctions.Services.Email;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Utils;
using System.Text.Json;

namespace FeedbackFunctions.Email;

/// <summary>
/// Azure Functions for processing digest email notifications
/// </summary>
public class DigestEmailProcessorFunction
{
    private readonly ILogger<DigestEmailProcessorFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly TableClient _userAccountsTableClient;
    private readonly TableClient _userRequestsTableClient;
    private readonly IReportCacheService _reportCacheService;
    private readonly IEmailService _emailService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DigestEmailProcessorFunction(
        ILogger<DigestEmailProcessorFunction> logger,
        IConfiguration configuration,
        IEmailService emailService,
        IReportCacheService reportCacheService)
    {
        _logger = logger;
        _emailService = emailService;
        _reportCacheService = reportCacheService;
        
#if DEBUG
        _configuration = new ConfigurationBuilder()
                    .AddJsonFile("local.settings.json")
                    .AddUserSecrets<Program>()
                    .Build();
#else
        _configuration = configuration;
#endif

        // Initialize table clients
        var storageConnection = _configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
        var tableServiceClient = new TableServiceClient(storageConnection);
        
        _userAccountsTableClient = tableServiceClient.GetTableClient("useraccounts");
        _userAccountsTableClient.CreateIfNotExists();
        
        _userRequestsTableClient = tableServiceClient.GetTableClient("userreportrequests");
        _userRequestsTableClient.CreateIfNotExists();
    }

    /// <summary>
    /// Weekly email processor - runs every Monday at 2:00 PM UTC (after reports are generated at 11:00 AM)
    /// Processes both individual report emails and weekly digest emails based on user preferences
    /// </summary>
    [Function("WeeklyEmailProcessor")]
    public async Task ProcessWeeklyEmails(
        [TimerTrigger("0 0 14 * * 1")] TimerInfo timer)
    {
        _logger.LogInformation("Starting weekly email processing at {Time}", DateTime.UtcNow);
        
        // Process individual report emails
        await ProcessIndividualReportEmails();
        
        // Process weekly digest emails
        await ProcessWeeklyDigestEmails();
    }

    /// <summary>
    /// Manual trigger for email processing (for testing)
    /// </summary>
    [Function("TriggerWeeklyEmailProcessing")]
    public async Task<HttpResponseData> TriggerWeeklyEmailProcessing(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "email/process-weekly")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Manual trigger for weekly email processing");
            
            // Process individual report emails
            await ProcessIndividualReportEmails();
            
            // Process weekly digest emails
            await ProcessWeeklyDigestEmails();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Weekly email processing completed" });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual weekly email processing trigger");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to process weekly emails");
            return errorResponse;
        }
    }

    /// <summary>
    /// Process individual report emails for users who want separate emails for each report
    /// </summary>
    private async Task ProcessIndividualReportEmails()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-7); // Only reports from the last week
            _logger.LogInformation("Processing individual report emails for reports since {CutoffDate}", cutoffDate);

            // Find all user report requests with email notifications enabled
            var filter = "EmailEnabled eq true";
            var emailsSent = 0;
            var usersProcessed = 0;

            await foreach (var userRequest in _userRequestsTableClient.QueryAsync<UserReportRequestModel>(filter))
            {
                try
                {
                    usersProcessed++;
                    var userId = userRequest.UserId;
                    
                    // Get user account to check tier, email settings, and email address
                    var userAccountEntity = await _userAccountsTableClient.GetEntityAsync<UserAccountEntity>(userId, "account");
                    
                    // Check if user's tier supports email notifications
                    if (!AccountTierUtils.SupportsEmailNotifications((AccountTier)userAccountEntity.Value.Tier))
                    {
                        _logger.LogDebug("Skipping emails for user {UserId} - tier {Tier} does not support email notifications", 
                            userId, (AccountTier)userAccountEntity.Value.Tier);
                        continue;
                    }
                    
                    // Check if user has email notifications enabled globally
                    if (!userAccountEntity.Value.EmailNotificationsEnabled)
                    {
                        _logger.LogDebug("Skipping emails for user {UserId} - email notifications disabled in account settings", userId);
                        continue;
                    }
                    
                    // Check if user wants individual emails (not digest)
                    if ((EmailReportFrequency)userAccountEntity.Value.EmailFrequency != EmailReportFrequency.Individual)
                    {
                        _logger.LogDebug("Skipping individual emails for user {UserId} - user prefers digest emails", userId);
                        continue;
                    }

                    // Get user's email address
                    var emailAddress = userAccountEntity.Value.PreferredEmail;

                    // Validate email address
                    if (!_emailService.IsValidEmailAddress(emailAddress))
                    {
                        _logger.LogWarning("Invalid email address for user {UserId}: {Email}", userId, emailAddress);
                        continue;
                    }

                    // Find recent reports for this specific user request
                    var reports = await GetRecentReportsForUserRequest(userRequest, cutoffDate);
                    
                    if (reports.Count == 0)
                    {
                        _logger.LogDebug("No recent reports found for user request {RequestId}", userRequest.Id);
                        continue;
                    }
                    

                    // Send individual email for each report
                    foreach (var report in reports)
                    {
                        var emailRequest = new FeedbackFunctions.Models.Email.ReportEmailRequest
                        {
                            RecipientEmail = emailAddress,
                            RecipientName = "User", // Could enhance with actual user name
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
                            emailsSent++;
                            _logger.LogInformation("Successfully sent individual report email to {Email} for report {ReportId}", 
                                emailAddress, report.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send individual report email to {Email} for report {ReportId}: {Error}", 
                                emailAddress, report.Id, deliveryStatus.ErrorMessage);
                        }
                    }

                    // Update last email sent timestamp
                    userAccountEntity.Value.LastEmailSent = DateTime.UtcNow;
                    await _userAccountsTableClient.UpdateEntityAsync(userAccountEntity.Value, userAccountEntity.Value.ETag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing individual report email for user request {RequestId} - {ErrorMessage}", userRequest.Id, ex.Message);
                }
            }

            _logger.LogInformation("Completed individual report email processing. Processed {UsersProcessed} user requests, sent {EmailsSent} emails", 
                usersProcessed, emailsSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in individual report email processing: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Process weekly digest emails for users who want all their reports combined into one email
    /// </summary>
    private async Task ProcessWeeklyDigestEmails()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-7); // Only reports from the last week
            _logger.LogInformation("Processing weekly digest emails for reports since {CutoffDate}", cutoffDate);

            // Group user requests by user ID for digest emails
            var digestRequests = new Dictionary<string, List<UserReportRequestModel>>();
            var filter = "EmailEnabled eq true";

            await foreach (var userRequest in _userRequestsTableClient.QueryAsync<UserReportRequestModel>(filter))
            {
                if (!digestRequests.ContainsKey(userRequest.UserId))
                {
                    digestRequests[userRequest.UserId] = new List<UserReportRequestModel>();
                }
                digestRequests[userRequest.UserId].Add(userRequest);
            }

            var emailsSent = 0;
            var usersProcessed = 0;

            foreach (var userDigestGroup in digestRequests)
            {
                try
                {
                    usersProcessed++;
                    var userId = userDigestGroup.Key;
                    var userRequests = userDigestGroup.Value;
                    
                    // Get user account to check tier, email settings, and email address
                    var userAccountEntity = await _userAccountsTableClient.GetEntityAsync<UserAccountEntity>(userId, "account");
                    
                    // Check if user's tier supports email notifications
                    if (!AccountTierUtils.SupportsEmailNotifications((AccountTier)userAccountEntity.Value.Tier))
                    {
                        _logger.LogDebug("Skipping digest for user {UserId} - tier {Tier} does not support email notifications", 
                            userId, (AccountTier)userAccountEntity.Value.Tier);
                        continue;
                    }
                    
                    // Check if user has email notifications enabled globally
                    if (!userAccountEntity.Value.EmailNotificationsEnabled)
                    {
                        _logger.LogDebug("Skipping digest for user {UserId} - email notifications disabled in account settings", userId);
                        continue;
                    }
                    
                    // Check if user wants digest emails (not individual)
                    if ((EmailReportFrequency)userAccountEntity.Value.EmailFrequency != EmailReportFrequency.WeeklyDigest)
                    {
                        _logger.LogDebug("Skipping digest for user {UserId} - user prefers individual emails", userId);
                        continue;
                    }
                    
                    // Get user's email address
                    var emailAddress = userAccountEntity.Value.PreferredEmail;

                    // Validate email address
                    if (!_emailService.IsValidEmailAddress(emailAddress))
                    {
                        _logger.LogWarning("Invalid email address for user {UserId}: {Email}", userId, emailAddress);
                        continue;
                    }

                    // Collect all reports for this user's digest
                    var allReports = new List<ReportModel>();
                    foreach (var userRequest in userRequests)
                    {
                        var reports = await GetRecentReportsForUserRequest(userRequest, cutoffDate);
                        allReports.AddRange(reports);
                    }
                    
                    if (allReports.Count == 0)
                    {
                        _logger.LogDebug("No recent reports found for user {UserId} digest", userId);
                        continue;
                    }

                    // Sort and limit reports for digest
                    allReports = allReports
                        .OrderByDescending(r => r.GeneratedAt)
                        .Take(10) // Limit to prevent huge emails
                        .ToList();

                    // Send weekly digest email
                    var deliveryStatus = await _emailService.SendWeeklyDigestAsync(
                        emailAddress, 
                        "User", // Could enhance with actual user name
                        allReports);

                    if (deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Succeeded || 
                        deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Running)
                    {
                        emailsSent++;
                        _logger.LogInformation("Successfully sent weekly digest to {Email} with {ReportCount} reports", 
                            emailAddress, allReports.Count);

                        // Update last email sent timestamp
                        userAccountEntity.Value.LastEmailSent = DateTime.UtcNow;
                        await _userAccountsTableClient.UpdateEntityAsync(userAccountEntity.Value, userAccountEntity.Value.ETag);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send weekly digest to {Email}: {Error}", 
                            emailAddress, deliveryStatus.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing weekly digest email for user {UserId}", userDigestGroup.Key);
                }
            }

            _logger.LogInformation("Completed weekly digest email processing. Processed {UsersProcessed} users, sent {EmailsSent} digest emails", 
                usersProcessed, emailsSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in weekly digest email processing");
        }
    }

    /// <summary>
    /// Get recent reports for a specific user report request
    /// </summary>
    private async Task<List<ReportModel>> GetRecentReportsForUserRequest(UserReportRequestModel userRequest, DateTime cutoffDate)
    {
        var reports = new List<ReportModel>();
        
        try
        {
            string source, subSource;
            
            if (userRequest.Type == "reddit")
            {
                source = "reddit";
                subSource = userRequest.Subreddit ?? "";
            }
            else if (userRequest.Type == "github")
            {
                source = "github";
                subSource = $"{userRequest.Owner}/{userRequest.Repo}";
            }
            else
            {
                _logger.LogWarning("Unknown request type {Type} for request {RequestId}", userRequest.Type, userRequest.Id);
                return reports;
            }
            
            // Get reports for this source/subsource combination
            var sourceReports = await _reportCacheService.GetReportsAsync(source, subSource);
            
            // Filter by cutoff date and add to results
            var recentReports = sourceReports
                .Where(r => r.GeneratedAt >= cutoffDate)
                .OrderByDescending(r => r.GeneratedAt)
                .Take(5) // Limit per source to prevent huge emails
                .ToList();
            
            reports.AddRange(recentReports);
            
            _logger.LogDebug("Found {ReportCount} recent reports for request {RequestId} ({Source}/{SubSource})", 
                recentReports.Count, userRequest.Id, source, subSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent reports for user request {RequestId}", userRequest.Id);
        }

        return reports;
    }
}