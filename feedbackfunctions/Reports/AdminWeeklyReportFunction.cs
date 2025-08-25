using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Email;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.Models.Email;
using SharedDump.Models.Account;
using SharedDump.Models.Reports;
using Microsoft.Extensions.Configuration;

namespace FeedbackFunctions.Reports;

/// <summary>
/// Azure Function to generate and send weekly admin reports to all administrators
/// Runs every Sunday at 9:00 AM UTC (early morning US time).
/// </summary>
public class AdminWeeklyReportFunction
{
    private readonly ILogger<AdminWeeklyReportFunction> _logger;
    private readonly IEmailService _emailService;
    private readonly IUserAccountService _userAccountService;
    private readonly IAdminReportConfigService _adminReportConfigService;
    private readonly IConfiguration _configuration;

    public AdminWeeklyReportFunction(
        ILogger<AdminWeeklyReportFunction> logger,
        IEmailService emailService,
        IUserAccountService userAccountService,
        IAdminReportConfigService adminReportConfigService,
        IConfiguration configuration)
    {
        _logger = logger;
        _emailService = emailService;
        _userAccountService = userAccountService;
        _adminReportConfigService = adminReportConfigService;
        _configuration = configuration;
    }

    /// <summary>
    /// Timer trigger for weekly admin report generation and distribution
    /// Runs every Sunday at 9:00 AM UTC
    /// </summary>
    [Function("ProcessAdminWeeklyReport")]
    public async Task ProcessAdminWeeklyReportAsync([TimerTrigger("0 0 9 * * 0")] TimerInfo timer)
    {
        _logger.LogInformation("Starting admin weekly report processing at {Time}", DateTime.UtcNow);

        try
        {
            // Get all admin users
            var adminUsers = await GetAdminUsersAsync();
            
            if (!adminUsers.Any())
            {
                _logger.LogWarning("No admin users found for weekly report distribution");
                return;
            }

            _logger.LogInformation("Found {AdminCount} admin users for weekly report", adminUsers.Count);

            // Generate dashboard metrics
            var dashboardMetrics = await _userAccountService.GetAdminDashboardMetricsAsync();
            
            // Calculate week ending date (previous Sunday)
            var weekEnding = DateTime.UtcNow.Date;
            while (weekEnding.DayOfWeek != DayOfWeek.Sunday)
            {
                weekEnding = weekEnding.AddDays(-1);
            }

            // Get additional report statistics
            var additionalStats = await GetAdditionalReportStatisticsAsync();

            var successCount = 0;
            var failureCount = 0;

            // Send report to each admin user
            foreach (var adminUser in adminUsers)
            {
                try
                {
                    var emailRequest = new AdminWeeklyReportEmailRequest
                    {
                        RecipientEmail = adminUser.PreferredEmail,
                        RecipientName = GetDisplayName(adminUser),
                        DashboardMetrics = dashboardMetrics,
                        ReportWeekEnding = weekEnding,
                        TotalActiveReportConfigs = additionalStats.ActiveReportConfigs,
                        ReportsGeneratedThisWeek = additionalStats.ReportsGeneratedThisWeek,
                        TopActiveRepositories = additionalStats.TopActiveRepositories,
                        TopActiveSubreddits = additionalStats.TopActiveSubreddits
                    };

                    var deliveryStatus = await _emailService.SendAdminWeeklyReportAsync(emailRequest);

                    if (deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Succeeded || 
                        deliveryStatus.Status == Azure.Communication.Email.EmailSendStatus.Running)
                    {
                        _logger.LogInformation("Successfully sent admin weekly report to {Email}", adminUser.PreferredEmail);
                        successCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send admin weekly report to {Email}: {Error}", 
                            adminUser.PreferredEmail, deliveryStatus.ErrorMessage);
                        failureCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending admin weekly report to {Email}", adminUser.PreferredEmail);
                    failureCount++;
                }
            }

            _logger.LogInformation("Admin weekly report processing completed. Success: {SuccessCount}, Failed: {FailureCount}", 
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in admin weekly report processing");
        }
    }

    /// <summary>
    /// Get all users with Admin tier
    /// </summary>
    private async Task<List<UserAccount>> GetAdminUsersAsync()
    {
        try
        {
            var allUsers = await _userAccountService.GetAllUserAccountsAsync();
            return allUsers.Where(u => u.Tier == AccountTier.Admin && !string.IsNullOrEmpty(u.PreferredEmail)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin users");
            return new List<UserAccount>();
        }
    }

    /// <summary>
    /// Get additional statistics for the admin report
    /// </summary>
    private async Task<AdditionalReportStatistics> GetAdditionalReportStatisticsAsync()
    {
        try
        {
            // Get active report configurations
            var activeConfigs = await _adminReportConfigService.GetAllActiveConfigsAsync();
            
            // Calculate week boundaries  
            var weekStart = DateTime.UtcNow.Date.AddDays(-7);
            var weekEnd = DateTime.UtcNow.Date;

            // Extract top repositories and subreddits from active configs
            var topRepositories = activeConfigs
                .Where(c => c.Type.Equals("github", StringComparison.OrdinalIgnoreCase) && 
                           !string.IsNullOrEmpty(c.Owner) && !string.IsNullOrEmpty(c.Repo))
                .GroupBy(c => $"{c.Owner}/{c.Repo}")
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            var topSubreddits = activeConfigs
                .Where(c => c.Type.Equals("reddit", StringComparison.OrdinalIgnoreCase) && 
                           !string.IsNullOrEmpty(c.Subreddit))
                .GroupBy(c => c.Subreddit!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            // For now, estimate reports generated this week based on active configs
            // In a future enhancement, this could query actual report generation logs
            var estimatedReportsThisWeek = activeConfigs.Count; // Simplified estimation

            return new AdditionalReportStatistics
            {
                ActiveReportConfigs = activeConfigs.Count,
                ReportsGeneratedThisWeek = estimatedReportsThisWeek,
                TopActiveRepositories = topRepositories,
                TopActiveSubreddits = topSubreddits
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving additional report statistics");
            return new AdditionalReportStatistics();
        }
    }

    /// <summary>
    /// Get display name for user (fallback to masked user ID if no name available)
    /// </summary>
    private static string GetDisplayName(UserAccount user)
    {
        if (!string.IsNullOrEmpty(user.PreferredEmail))
        {
            var emailParts = user.PreferredEmail.Split('@');
            if (emailParts.Length > 0)
                return emailParts[0]; // Use email username part
        }
        
        // Fallback to masked user ID
        return user.UserId.Length > 8 ? $"{user.UserId[..4]}...{user.UserId[^4..]}" : user.UserId;
    }
}

/// <summary>
/// Additional statistics for admin reports
/// </summary>
public class AdditionalReportStatistics
{
    public int ActiveReportConfigs { get; set; }
    public int ReportsGeneratedThisWeek { get; set; }
    public List<string> TopActiveRepositories { get; set; } = new();
    public List<string> TopActiveSubreddits { get; set; } = new();
}