using SharedDump.Models.Admin;
using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of IAdminDashboardService for development and testing.
/// Returns realistic sample data for admin dashboard metrics.
/// </summary>
public class MockAdminDashboardService : IAdminDashboardService
{
    private readonly ILogger<MockAdminDashboardService> _logger;

    public MockAdminDashboardService(ILogger<MockAdminDashboardService> logger)
    {
        _logger = logger;
    }

    public async Task<AdminDashboardMetrics> GetDashboardMetricsAsync()
    {
        await Task.Delay(500); // Simulate network delay
        
        _logger.LogInformation("Mock: Returning sample admin dashboard metrics");

        return new AdminDashboardMetrics
        {
            UserStats = new UserStatistics
            {
                TotalUsers = 157,
                NewUsersLast7Days = 12,
                NewUsersLast30Days = 43,
                TierDistribution = new Dictionary<string, int>
                {
                    ["Free"] = 120,
                    ["Pro"] = 28,
                    ["ProPlus"] = 7,
                    ["Admin"] = 2
                },
                TierDistributionPercentage = new Dictionary<string, double>
                {
                    ["Free"] = 76.4,
                    ["Pro"] = 17.8,
                    ["ProPlus"] = 4.5,
                    ["Admin"] = 1.3
                },
                EmailNotificationsEnabledCount = 45,
                EmailNotificationsEnabledPercentage = 28.7,
                EmailNotificationsByTier = new Dictionary<string, int>
                {
                    ["Pro"] = 18,
                    ["ProPlus"] = 7,
                    ["Admin"] = 2
                },
                ApiEnabledUsers = 15,
                ApiEnabledUsersPercentage = 9.6,
                ActiveUsersLast14Days = 89,
                InactiveUsers = 68,
                ActiveUsersPercentage = 56.7
            },
            UsageStats = new UsageStatistics
            {
                TotalAnalysesUsed = 1247,
                TotalFeedQueriesUsed = 3456,
                TotalActiveReports = 156,
                TotalApiCalls = 789,
                TierUsageMetrics = new Dictionary<string, TierUsageMetrics>
                {
                    ["Free"] = new()
                    {
                        UserCount = 120,
                        TierLimit = 10,
                        TotalUsed = 687,
                        UtilizationPercentage = 57.3,
                        UsageType = "Analyses"
                    },
                    ["Pro"] = new()
                    {
                        UserCount = 28,
                        TierLimit = 75,
                        TotalUsed = 421,
                        UtilizationPercentage = 20.0,
                        UsageType = "Analyses"
                    },
                    ["ProPlus"] = new()
                    {
                        UserCount = 7,
                        TierLimit = 300,
                        TotalUsed = 139,
                        UtilizationPercentage = 6.6,
                        UsageType = "Analyses"
                    }
                },
                TopUsers = new List<TopUserUsage>
                {
                    new() { UserId = "user-001", MaskedUserId = "use***001", Tier = "ProPlus", CombinedUsage = 425, AnalysesUsed = 89, FeedQueriesUsed = 256, ApiUsed = 80 },
                    new() { UserId = "user-002", MaskedUserId = "use***002", Tier = "Pro", CombinedUsage = 312, AnalysesUsed = 67, FeedQueriesUsed = 189, ApiUsed = 56 },
                    new() { UserId = "user-003", MaskedUserId = "use***003", Tier = "ProPlus", CombinedUsage = 278, AnalysesUsed = 45, FeedQueriesUsed = 178, ApiUsed = 55 },
                    new() { UserId = "user-004", MaskedUserId = "use***004", Tier = "Pro", CombinedUsage = 245, AnalysesUsed = 58, FeedQueriesUsed = 156, ApiUsed = 31 },
                    new() { UserId = "user-005", MaskedUserId = "use***005", Tier = "Free", CombinedUsage = 203, AnalysesUsed = 9, FeedQueriesUsed = 194, ApiUsed = 0 }
                }
            },
            ApiStats = new ApiStatistics
            {
                TotalApiEnabledUsers = 15,
                TotalApiCalls = 789,
                AverageApiCallsPerUser = 52.6,
                RecentlyActiveApiUsers = 9,
                TopApiUsers = new List<TopApiUser>
                {
                    new() { UserId = "user-001", MaskedUserId = "use***001", Tier = "ProPlus", ApiCalls = 80, LastUsedAt = DateTime.UtcNow.AddHours(-2) },
                    new() { UserId = "user-002", MaskedUserId = "use***002", Tier = "Pro", ApiCalls = 56, LastUsedAt = DateTime.UtcNow.AddHours(-6) },
                    new() { UserId = "user-003", MaskedUserId = "use***003", Tier = "ProPlus", ApiCalls = 55, LastUsedAt = DateTime.UtcNow.AddDays(-1) },
                    new() { UserId = "user-004", MaskedUserId = "use***004", Tier = "Pro", ApiCalls = 31, LastUsedAt = DateTime.UtcNow.AddDays(-3) },
                    new() { UserId = "user-006", MaskedUserId = "use***006", Tier = "Pro", ApiCalls = 28, LastUsedAt = DateTime.UtcNow.AddDays(-2) }
                },
                EndpointDistribution = new Dictionary<string, int>
                {
                    ["AnalysisReport"] = 345,
                    ["FeedQuery"] = 234,
                    ["SharedAnalysis"] = 156,
                    ["UserMetrics"] = 54
                }
            },
            GeneratedAt = DateTime.UtcNow
        };
    }
}