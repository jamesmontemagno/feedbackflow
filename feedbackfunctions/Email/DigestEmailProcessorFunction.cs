using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;
using SharedDump.Models.Account;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using FeedbackFunctions.Services.Email;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Services.Reports;
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
    }

    /// <summary>
    /// Daily digest processor - runs every day at 9:00 AM UTC
    /// </summary>
    [Function("DailyDigestProcessor")]
    public async Task ProcessDailyDigests(
        [TimerTrigger("0 0 9 * * *")] TimerInfo timer)
    {
        _logger.LogInformation("Starting daily digest processing at {Time}", DateTime.UtcNow);
        await ProcessDigestEmails(EmailReportFrequency.Daily);
    }

    /// <summary>
    /// Weekly digest processor - runs every Monday at 9:00 AM UTC
    /// </summary>
    [Function("WeeklyDigestProcessor")]
    public async Task ProcessWeeklyDigests(
        [TimerTrigger("0 0 9 * * 1")] TimerInfo timer)
    {
        _logger.LogInformation("Starting weekly digest processing at {Time}", DateTime.UtcNow);
        await ProcessDigestEmails(EmailReportFrequency.Weekly);
    }

    /// <summary>
    /// Manual trigger for digest processing (for testing)
    /// </summary>
    [Function("TriggerDigestProcessing")]
    public async Task<HttpResponseData> TriggerDigestProcessing(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "email/digest/{frequency}")] HttpRequestData req,
        string frequency)
    {
        try
        {
            if (!Enum.TryParse<EmailReportFrequency>(frequency, true, out var emailFrequency) ||
                emailFrequency == EmailReportFrequency.None || emailFrequency == EmailReportFrequency.Immediate)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid frequency. Use 'Daily' or 'Weekly'.");
                return badRequestResponse;
            }

            _logger.LogInformation("Manual trigger for {Frequency} digest processing", emailFrequency);
            await ProcessDigestEmails(emailFrequency);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = $"{emailFrequency} digest processing completed" });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual digest processing trigger");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to process digest emails");
            return errorResponse;
        }
    }

    /// <summary>
    /// Process digest emails for users with the specified frequency
    /// </summary>
    private async Task ProcessDigestEmails(EmailReportFrequency frequency)
    {
        try
        {
            var cutoffDate = frequency == EmailReportFrequency.Daily 
                ? DateTime.UtcNow.AddDays(-1) 
                : DateTime.UtcNow.AddDays(-7);

            _logger.LogInformation("Processing {Frequency} digest emails for reports since {CutoffDate}", 
                frequency, cutoffDate);

            // Find users who want digest emails at this frequency
            var filter = $"EmailFrequency eq {(int)frequency} and EmailNotificationsEnabled eq true";
            var usersProcessed = 0;
            var emailsSent = 0;

            await foreach (var userAccountEntity in _userAccountsTableClient.QueryAsync<UserAccountEntity>(filter))
            {
                try
                {
                    usersProcessed++;
                    var userId = userAccountEntity.PartitionKey;
                    
                    // Get user's email address
                    var emailAddress = !string.IsNullOrEmpty(userAccountEntity.PreferredEmail) 
                        ? userAccountEntity.PreferredEmail 
                        : userId;

                    // Validate email address
                    if (!_emailService.IsValidEmailAddress(emailAddress))
                    {
                        _logger.LogWarning("Invalid email address for user {UserId}: {Email}", 
                            userId, emailAddress);
                        continue;
                    }

                    // Check if we should send digest (avoid sending too frequently)
                    if (userAccountEntity.LastEmailSent.HasValue && frequency == EmailReportFrequency.Daily)
                    {
                        var timeSinceLastEmail = DateTime.UtcNow - userAccountEntity.LastEmailSent.Value;
                        if (timeSinceLastEmail < TimeSpan.FromHours(20)) // Don't send daily digest more than once per 20 hours
                        {
                            _logger.LogDebug("Skipping daily digest for {UserId} - last email sent {TimeSince} ago", 
                                userId, timeSinceLastEmail);
                            continue;
                        }
                    }

                    // Find recent reports for this user
                    var userReports = await GetRecentReportsForUser(userId, cutoffDate);
                    
                    if (userReports.Count == 0)
                    {
                        _logger.LogDebug("No recent reports found for user {UserId}", userId);
                        continue;
                    }

                    // Send digest email
                    var deliveryStatus = await _emailService.SendWeeklyDigestAsync(
                        emailAddress, 
                        "User", // We could enhance this with actual user name
                        userReports);

                    if (deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Succeeded || 
                        deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Running)
                    {
                        emailsSent++;
                        _logger.LogInformation("Successfully sent {Frequency} digest to {Email} with {ReportCount} reports", 
                            frequency, emailAddress, userReports.Count);

                        // Update last email sent timestamp
                        userAccountEntity.LastEmailSent = DateTime.UtcNow;
                        await _userAccountsTableClient.UpdateEntityAsync(userAccountEntity, userAccountEntity.ETag);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send {Frequency} digest to {Email}: {Error}", 
                            frequency, emailAddress, deliveryStatus.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing digest email for user {UserId}", userAccountEntity.PartitionKey);
                }
            }

            _logger.LogInformation("Completed {Frequency} digest processing. Processed {UsersProcessed} users, sent {EmailsSent} emails", 
                frequency, usersProcessed, emailsSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Frequency} digest email processing", frequency);
        }
    }

    /// <summary>
    /// Get recent reports for a specific user
    /// </summary>
    private async Task<List<ReportModel>> GetRecentReportsForUser(string userId, DateTime cutoffDate)
    {
        var reports = new List<ReportModel>();
        
        try
        {
            // Use the report cache service to get all reports, then filter by date
            var allReports = await _reportCacheService.GetReportsAsync(sourceFilter: null, subsourceFilter: null);
            
            foreach (var report in allReports)
            {
                // Filter reports generated since cutoff date
                if (report.GeneratedAt >= cutoffDate)
                {
                    reports.Add(report);
                    
                    // Limit to prevent very large digest emails
                    if (reports.Count >= 10)
                        break;
                }
            }
            
            // Sort by most recent first
            reports = reports.OrderByDescending(r => r.GeneratedAt).ToList();
            
            _logger.LogDebug("Found {ReportCount} recent reports for user {UserId} since {CutoffDate}", 
                reports.Count, userId, cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent reports for user {UserId}", userId);
        }

        return reports;
    }
}