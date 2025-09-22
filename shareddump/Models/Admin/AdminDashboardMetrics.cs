using System;
using System.Collections.Generic;

namespace SharedDump.Models.Admin;

/// <summary>
/// Model for admin dashboard metrics aggregated from existing data sources
/// </summary>
public class AdminDashboardMetrics
{
    public UserStatistics UserStats { get; set; } = new();
    public UsageStatistics UsageStats { get; set; } = new();
    public ApiStatistics ApiStats { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User-related statistics for admin dashboard
/// </summary>
public class UserStatistics
{
    public int TotalUsers { get; set; }
    public int NewUsersLast7Days { get; set; }
    public int NewUsersLast30Days { get; set; }
    public Dictionary<string, int> TierDistribution { get; set; } = new();
    public Dictionary<string, double> TierDistributionPercentage { get; set; } = new();
    public int EmailNotificationsEnabledCount { get; set; }
    public double EmailNotificationsEnabledPercentage { get; set; }
    public Dictionary<string, int> EmailNotificationsByTier { get; set; } = new();
    public int ApiEnabledUsers { get; set; }
    public double ApiEnabledUsersPercentage { get; set; }
    public int ActiveUsersLast14Days { get; set; }
    public int InactiveUsers { get; set; }
    public double ActiveUsersPercentage { get; set; }
}

/// <summary>
/// Usage-related statistics for admin dashboard
/// </summary>
public class UsageStatistics
{
    public int TotalAnalysesUsed { get; set; }
    public int TotalFeedQueriesUsed { get; set; }
    public int TotalActiveReports { get; set; }
    public int TotalApiCalls { get; set; }
    public Dictionary<string, TierUsageMetrics> TierUsageMetrics { get; set; } = new();
    public List<TopUserUsage> TopUsers { get; set; } = new();
    public List<TopReport> TopReports { get; set; } = new();
}

/// <summary>
/// Usage metrics for a specific tier
/// </summary>
public class TierUsageMetrics
{
    public int UserCount { get; set; }
    public int TierLimit { get; set; }
    public int TotalUsed { get; set; }
    public double UtilizationPercentage { get; set; }
    public string UsageType { get; set; } = string.Empty;
}

/// <summary>
/// Top user usage information
/// </summary>
public class TopUserUsage
{
    public string UserId { get; set; } = string.Empty;
    public string MaskedUserId { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int CombinedUsage { get; set; }
    public int AnalysesUsed { get; set; }
    public int FeedQueriesUsed { get; set; }
    public int ApiUsed { get; set; }
}

/// <summary>
/// API adoption statistics for admin dashboard
/// </summary>
public class ApiStatistics
{
    public int TotalApiEnabledUsers { get; set; }
    public int TotalApiCalls { get; set; }
    public double AverageApiCallsPerUser { get; set; }
    public int RecentlyActiveApiUsers { get; set; }
    public List<TopApiUser> TopApiUsers { get; set; } = new();
    public Dictionary<string, int> EndpointDistribution { get; set; } = new();
}

/// <summary>
/// Top API user information
/// </summary>
public class TopApiUser
{
    public string UserId { get; set; } = string.Empty;
    public string MaskedUserId { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int ApiCalls { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Top report subscription information
/// </summary>
public class TopReport
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // subreddit or owner/repo
    public int SubscriberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}