using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedDump.Models.Account;
using SharedDump.Models.Admin;

namespace FeedbackFunctions.Services.Account;

/// <summary>
/// Unified service for user account management, usage tracking, and limits validation
/// </summary>
public interface IUserAccountService
{
    // User Account Management
    Task<UserAccount?> GetUserAccountAsync(string userId);
    Task UpsertUserAccountAsync(UserAccount userAccount);
    Task<bool> DeleteUserAccountAsync(string userId);
    Task<int> ResetAllMonthlyUsageAsync();

    // Limits and Validation
    AccountLimits GetLimitsForTier(AccountTier tier);
    Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType);
    Task<bool> CanPerformActionAsync(string userId, UsageType usageType);

    // Usage Tracking
    Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null, int amount = 1);
    Task<List<UsageRecord>> GetUsageHistoryAsync(string userId);

    // Convenience Methods
    Task<AccountLimits> GetUserLimitsAsync(string userId);
    Task RefreshUsageLimitsAsync(string userId);

    // Table Management
    Task InitializeTablesAsync();

    /// <summary>
    /// Returns a dictionary of userId to AccountTier for all users.
    /// </summary>
    Task<Dictionary<string, SharedDump.Models.Account.AccountTier>> GetAllUserTiersAsync();

    // Admin Dashboard Methods
    /// <summary>
    /// Get all user accounts for admin dashboard (admin only)
    /// </summary>
    Task<List<UserAccount>> GetAllUserAccountsAsync();

    /// <summary>
    /// Get recent usage records for activity analysis (admin only)
    /// </summary>
    Task<List<UsageRecord>> GetRecentUsageRecordsAsync(DateTime since);

    /// <summary>
    /// Generate comprehensive admin dashboard metrics (admin only)
    /// </summary>
    Task<AdminDashboardMetrics> GetAdminDashboardMetricsAsync();
}
