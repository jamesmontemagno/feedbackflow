using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Account;
using SharedDump.Models.Admin;

namespace FeedbackFunctions.Services.Account;

public class UserAccountService : IUserAccountService
{
    private readonly TableClient _userAccountsTable;
    private readonly TableClient _usageRecordsTable;
    private readonly TableClient _apiKeysTable;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<UserAccountService>? _logger;

    private const string UserAccountsTableName = "UserAccounts";
    private const string UsageRecordsTableName = "UsageRecords";
    private const string ApiKeysTableName = "apikeys";

    public UserAccountService(string storageConnectionString, IConfiguration? configuration = null, ILogger<UserAccountService>? logger = null)
    {
        _userAccountsTable = new TableClient(storageConnectionString, UserAccountsTableName);
        _usageRecordsTable = new TableClient(storageConnectionString, UsageRecordsTableName);
        _apiKeysTable = new TableClient(storageConnectionString, ApiKeysTableName);
        _configuration = configuration;
        _logger = logger;
    }

    #region User Account Management

    public async Task<UserAccount?> GetUserAccountAsync(string userId)
    {
        try
        {
            var response = await _userAccountsTable.GetEntityIfExistsAsync<UserAccountEntity>(userId, "account");
            if (!response.HasValue) return null;

            return MapEntityToModel(response.Value!);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving user account for {UserId}", userId);
            return null;
        }
    }

    public async Task UpsertUserAccountAsync(UserAccount userAccount)
    {
        var entity = MapModelToEntity(userAccount);
        await _userAccountsTable.UpsertEntityAsync(entity);
    }

    public async Task<bool> DeleteUserAccountAsync(string userId)
    {
        try
        {
            var entity = await _userAccountsTable.GetEntityIfExistsAsync<UserAccountEntity>(userId, "account");
            if (!entity.HasValue)
                return false;

            // Delete associated API keys first
            try
            {
                var filter = $"UserId eq '{userId}'";
                await foreach (var apiKeyEntity in _apiKeysTable.QueryAsync<ApiKeyEntity>(filter))
                {
                    await _apiKeysTable.DeleteEntityAsync(apiKeyEntity);
                    _logger?.LogInformation("Deleted API key for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error deleting API keys for user {UserId}", userId);
                // Continue with account deletion even if API key deletion fails
            }

            await _userAccountsTable.DeleteEntityAsync(entity.Value);
            _logger?.LogInformation("User account {UserId} deleted permanently", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting user account for {UserId}", userId);
            return false;
        }
    }

    public async Task<Dictionary<string, SharedDump.Models.Account.AccountTier>> GetAllUserTiersAsync()
    {
        var userTiers = new Dictionary<string, SharedDump.Models.Account.AccountTier>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await foreach (var entity in _userAccountsTable.QueryAsync<UserAccountEntity>())
            {
                var userId = entity.PartitionKey;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    userTiers[userId] = (SharedDump.Models.Account.AccountTier)entity.Tier;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error querying all user tiers");
        }
        return userTiers;
    }

    public async Task<int> ResetAllMonthlyUsageAsync()
    {
        var resetCount = 0;
        var resetDate = DateTime.UtcNow;

        try
        {
            await foreach (var user in _userAccountsTable.QueryAsync<UserAccountEntity>())
            {
                user.AnalysesUsed = 0;
                user.FeedQueriesUsed = 0;
                user.ApiUsed = 0;
                user.LastResetDate = resetDate;
                
                await _userAccountsTable.UpdateEntityAsync(user, user.ETag);
                resetCount++;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error resetting monthly usage");
            throw;
        }

        return resetCount;
    }

    #endregion

    #region Limits and Validation

    public AccountLimits GetLimitsForTier(AccountTier tier)
    {
        return tier switch
        {
            AccountTier.Free => new AccountLimits 
            { 
                AnalysisLimit = GetConfigValue("AccountTiers:Free:AnalysisLimit", 10),
                ReportLimit = GetConfigValue("AccountTiers:Free:ReportLimit", 1),
                FeedQueryLimit = GetConfigValue("AccountTiers:Free:FeedQueryLimit", 20),
                ApiLimit = 0, // Free tier doesn't support API access
                AnalysisRetentionDays = GetConfigValue("AccountTiers:Free:AnalysisRetentionDays", 30)
            },
            AccountTier.Pro => new AccountLimits 
            { 
                AnalysisLimit = GetConfigValue("AccountTiers:Pro:AnalysisLimit", 75),
                ReportLimit = GetConfigValue("AccountTiers:Pro:ReportLimit", 5),
                FeedQueryLimit = GetConfigValue("AccountTiers:Pro:FeedQueryLimit", 200),
                ApiLimit = 0, // Pro tier doesn't support API access
                AnalysisRetentionDays = GetConfigValue("AccountTiers:Pro:AnalysisRetentionDays", 60)
            },
            AccountTier.ProPlus => new AccountLimits 
            { 
                AnalysisLimit = GetConfigValue("AccountTiers:ProPlus:AnalysisLimit", 300),
                ReportLimit = GetConfigValue("AccountTiers:ProPlus:ReportLimit", 25),
                FeedQueryLimit = GetConfigValue("AccountTiers:ProPlus:FeedQueryLimit", 1000),
                ApiLimit = GetConfigValue("AccountTiers:ProPlus:ApiLimit", 100),
                AnalysisRetentionDays = GetConfigValue("AccountTiers:ProPlus:AnalysisRetentionDays", 90)
            },
            AccountTier.SuperUser => new AccountLimits 
            { 
                AnalysisLimit = 10000,
                ReportLimit = 10000,
                FeedQueryLimit = 10000,
                ApiLimit = 1000,
                AnalysisRetentionDays = 3650 // 10 years
            },
            AccountTier.Admin => new AccountLimits 
            { 
                AnalysisLimit = 10000,
                ReportLimit = 10000,
                FeedQueryLimit = 10000,
                ApiLimit = 1000,
                AnalysisRetentionDays = 3650 // 10 years
            },
            _ => new AccountLimits { AnalysisLimit = 0, ReportLimit = 0, FeedQueryLimit = 0, ApiLimit = 0, AnalysisRetentionDays = 0 }
        };
    }

    public async Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType)
    {
        var user = await GetUserAccountAsync(userId);
        
        // If user account doesn't exist, they haven't registered yet
        if (user == null)
        {
            return new UsageValidationResult 
            { 
                IsWithinLimit = false, 
                UsageType = usageType,
                CurrentUsage = 0,
                Limit = 0,
                CurrentTier = AccountTier.Free,
                ResetDate = DateTime.UtcNow.AddDays(30),
                ErrorMessage = "User account not found. Please register first.",
                UpgradeUrl = null
            };
        }
        
        var limits = GetLimitsForTier(user.Tier);
        
        bool withinLimit = usageType switch
        {
            UsageType.Analysis => user.AnalysesUsed < limits.AnalysisLimit,
            UsageType.FeedQuery => user.FeedQueriesUsed < limits.FeedQueryLimit,
            UsageType.ReportCreated => user.ActiveReports < limits.ReportLimit,
            UsageType.ApiCall => user.ApiUsed < limits.ApiLimit,
            _ => true
        };

        return new UsageValidationResult 
        { 
            IsWithinLimit = withinLimit, 
            UsageType = usageType,
            CurrentUsage = GetCurrentUsage(user, usageType),
            Limit = GetLimitForUsageType(limits, usageType),
            CurrentTier = user.Tier,
            ResetDate = usageType == UsageType.ReportCreated ? null : user.LastResetDate.AddDays(30),
            ErrorMessage = withinLimit ? null : $"Usage limit exceeded for {usageType}",
            UpgradeUrl = "/account-settings"
        };
    }

    public async Task<bool> CanPerformActionAsync(string userId, UsageType usageType)
    {
        var result = await ValidateUsageAsync(userId, usageType);
        return result.IsWithinLimit;
    }

    #endregion

    #region Usage Tracking

    public async Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null, int amount = 1)
    {
        var user = await GetUserAccountAsync(userId);
        
        // If user account doesn't exist, they haven't registered yet - don't track usage
        if (user == null)
        {
            _logger?.LogWarning("Cannot track usage for user {UserId} - account not found. User must register first.", userId);
            return;
        }

        // Update usage counters
        switch (usageType)
        {
            case UsageType.Analysis:
                user.AnalysesUsed++;
                break;
            case UsageType.FeedQuery:
                user.FeedQueriesUsed++;
                break;
            case UsageType.ReportCreated:
                user.ActiveReports++;
                break;
            case UsageType.ReportDeleted:
                user.ActiveReports = Math.Max(0, user.ActiveReports - 1);
                break;
            case UsageType.ApiCall:
                user.ApiUsed += amount;
                break;
        }

        await UpsertUserAccountAsync(user);

        // Add usage record for audit trail
        var usageRecord = new UsageRecordEntity
        {
            PartitionKey = userId,
            RowKey = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{usageType}_{Guid.NewGuid():N}",
            UsageType = (int)usageType,
            ResourceId = resourceId ?? string.Empty,
            Details = string.Empty,
            UsageTimestamp = DateTime.UtcNow
        };

        try
        {
            await _usageRecordsTable.AddEntityAsync(usageRecord);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to record usage audit trail for {UserId}, {UsageType}", userId, usageType);
            // Don't fail the main operation if audit trail fails
        }
    }

    public async Task<List<UsageRecord>> GetUsageHistoryAsync(string userId)
    {
        var results = new List<UsageRecord>();
        
        try
        {
            await foreach (var entity in _usageRecordsTable.QueryAsync<UsageRecordEntity>(e => e.PartitionKey == userId))
            {
                results.Add(new UsageRecord
                {
                    UserId = entity.PartitionKey,
                    Date = entity.UsageTimestamp,
                    Type = (UsageType)entity.UsageType,
                    ResourceId = entity.ResourceId,
                    Details = entity.Details
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving usage history for {UserId}", userId);
        }

        return results.OrderByDescending(r => r.Date).ToList();
    }

    #endregion

    #region Convenience Methods

    public async Task<AccountLimits> GetUserLimitsAsync(string userId)
    {
        var user = await GetUserAccountAsync(userId);
        
        // If user account doesn't exist, return default Free tier limits
        if (user == null)
        {
            return GetLimitsForTier(AccountTier.Free);
        }
        
        return GetLimitsForTier(user.Tier);
    }

    public async Task RefreshUsageLimitsAsync(string userId)
    {
        // Limits are calculated dynamically based on tier, so no refresh needed
        // This method is kept for API compatibility
        await Task.CompletedTask;
    }

    #endregion

    #region Table Management

    public async Task InitializeTablesAsync()
    {
        await _userAccountsTable.CreateIfNotExistsAsync();
        await _usageRecordsTable.CreateIfNotExistsAsync();
        await _apiKeysTable.CreateIfNotExistsAsync();
    }

    #endregion

    #region Private Helper Methods

    private int GetConfigValue(string key, int defaultValue)
    {
        if (_configuration == null) return defaultValue;
        
        var value = _configuration[key];
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int GetCurrentUsage(UserAccount user, UsageType usageType)
    {
        return usageType switch
        {
            UsageType.Analysis => user.AnalysesUsed,
            UsageType.FeedQuery => user.FeedQueriesUsed,
            UsageType.ReportCreated => user.ActiveReports,
            UsageType.ApiCall => user.ApiUsed,
            _ => 0
        };
    }

    private static int GetLimitForUsageType(AccountLimits limits, UsageType usageType)
    {
        return usageType switch
        {
            UsageType.Analysis => limits.AnalysisLimit,
            UsageType.FeedQuery => limits.FeedQueryLimit,
            UsageType.ReportCreated => limits.ReportLimit,
            UsageType.ApiCall => limits.ApiLimit,
            _ => 0
        };
    }

    private static UserAccount MapEntityToModel(UserAccountEntity entity)
    {
        return new UserAccount
        {
            UserId = entity.PartitionKey,
            Tier = (AccountTier)entity.Tier,
            SubscriptionStart = entity.SubscriptionStart,
            SubscriptionEnd = entity.SubscriptionEnd,
            CreatedAt = entity.CreatedAt,
            LastResetDate = entity.LastResetDate,
            AnalysesUsed = entity.AnalysesUsed,
            FeedQueriesUsed = entity.FeedQueriesUsed,
            ActiveReports = entity.ActiveReports,
            ApiUsed = entity.ApiUsed,
            PreferredEmail = entity.PreferredEmail,
            EmailFrequency = (EmailReportFrequency)entity.EmailFrequency,
            EmailNotificationsEnabled = entity.EmailNotificationsEnabled,
            LastEmailSent = entity.LastEmailSent
        };
    }

    private static UserAccountEntity MapModelToEntity(UserAccount model)
    {
        return new UserAccountEntity
        {
            PartitionKey = model.UserId,
            RowKey = "account",
            Tier = (int)model.Tier,
            SubscriptionStart = model.SubscriptionStart,
            SubscriptionEnd = model.SubscriptionEnd,
            CreatedAt = model.CreatedAt,
            LastResetDate = model.LastResetDate,
            AnalysesUsed = model.AnalysesUsed,
            FeedQueriesUsed = model.FeedQueriesUsed,
            ActiveReports = model.ActiveReports,
            ApiUsed = model.ApiUsed,
            PreferredEmail = model.PreferredEmail,
            EmailFrequency = (int)model.EmailFrequency,
            EmailNotificationsEnabled = model.EmailNotificationsEnabled,
            LastEmailSent = model.LastEmailSent
        };
    }

    #endregion

    #region Admin Dashboard Methods

    public async Task<List<UserAccount>> GetAllUserAccountsAsync()
    {
        var accounts = new List<UserAccount>();
        
        try
        {
            await foreach (var entity in _userAccountsTable.QueryAsync<UserAccountEntity>())
            {
                accounts.Add(MapEntityToModel(entity));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving all user accounts for admin dashboard");
        }

        return accounts;
    }

    public async Task<List<UsageRecord>> GetRecentUsageRecordsAsync(DateTime since)
    {
        var records = new List<UsageRecord>();
        
        try
        {
            // Query with date filter to improve performance
            var filter = $"UsageTimestamp ge datetime'{since:yyyy-MM-ddTHH:mm:ssZ}'";
            await foreach (var entity in _usageRecordsTable.QueryAsync<UsageRecordEntity>(filter))
            {
                records.Add(new UsageRecord
                {
                    UserId = entity.PartitionKey,
                    Date = entity.UsageTimestamp,
                    Type = (UsageType)entity.UsageType,
                    ResourceId = entity.ResourceId,
                    Details = entity.Details
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving recent usage records since {Since}", since);
        }

        return records;
    }

    public async Task<AdminDashboardMetrics> GetAdminDashboardMetricsAsync()
    {
        var metrics = new AdminDashboardMetrics();
        
        try
        {
            // Get all user accounts and API keys
            var allUsers = await GetAllUserAccountsAsync();
            var apiKeys = await GetAllApiKeysAsync();
            
            // Calculate cutoff dates
            var last7Days = DateTime.UtcNow.AddDays(-7);
            var last30Days = DateTime.UtcNow.AddDays(-30);
            var last14Days = DateTime.UtcNow.AddDays(-14);
            
            // Get recent usage for activity analysis
            var recentUsage = await GetRecentUsageRecordsAsync(last14Days);
            var activeUserIds = recentUsage.Select(r => r.UserId).Distinct().ToHashSet();

            // Calculate user statistics
            metrics.UserStats = CalculateUserStatistics(allUsers, apiKeys, activeUserIds, last7Days, last30Days);
            
            // Calculate usage statistics
            metrics.UsageStats = CalculateUsageStatistics(allUsers);
            
            // Calculate API statistics
            metrics.ApiStats = CalculateApiStatistics(allUsers, apiKeys, last7Days);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating admin dashboard metrics");
        }

        return metrics;
    }

    private async Task<List<ApiKey>> GetAllApiKeysAsync()
    {
        var apiKeys = new List<ApiKey>();
        
        try
        {
            await foreach (var entity in _apiKeysTable.QueryAsync<ApiKeyEntity>())
            {
                apiKeys.Add(new ApiKey
                {
                    Key = entity.RowKey,
                    UserId = entity.UserId,
                    IsEnabled = entity.IsEnabled,
                    CreatedAt = entity.CreatedAt,
                    LastUsedAt = entity.LastUsedAt,
                    Name = entity.Name
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving all API keys");
        }

        return apiKeys;
    }

    private UserStatistics CalculateUserStatistics(List<UserAccount> allUsers, List<ApiKey> apiKeys, HashSet<string> activeUserIds, DateTime last7Days, DateTime last30Days)
    {
        var stats = new UserStatistics
        {
            TotalUsers = allUsers.Count,
            NewUsersLast7Days = allUsers.Count(u => u.CreatedAt >= last7Days),
            NewUsersLast30Days = allUsers.Count(u => u.CreatedAt >= last30Days),
            ActiveUsersLast14Days = activeUserIds.Count,
            InactiveUsers = allUsers.Count - activeUserIds.Count
        };

        if (stats.TotalUsers > 0)
        {
            stats.ActiveUsersPercentage = Math.Round((double)stats.ActiveUsersLast14Days / stats.TotalUsers * 100, 1);
        }

        // Calculate tier distribution
        var tierGroups = allUsers.GroupBy(u => u.Tier.ToString()).ToList();
        foreach (var group in tierGroups)
        {
            var count = group.Count();
            stats.TierDistribution[group.Key] = count;
            stats.TierDistributionPercentage[group.Key] = stats.TotalUsers > 0 ? Math.Round((double)count / stats.TotalUsers * 100, 1) : 0;
        }

        // Calculate email notification stats
        stats.EmailNotificationsEnabledCount = allUsers.Count(u => u.EmailNotificationsEnabled);
        stats.EmailNotificationsEnabledPercentage = stats.TotalUsers > 0 ? Math.Round((double)stats.EmailNotificationsEnabledCount / stats.TotalUsers * 100, 1) : 0;

        foreach (var group in allUsers.Where(u => u.EmailNotificationsEnabled).GroupBy(u => u.Tier.ToString()))
        {
            stats.EmailNotificationsByTier[group.Key] = group.Count();
        }

        // Calculate API-enabled users
        var apiEnabledUserIds = apiKeys.Where(k => k.IsEnabled).Select(k => k.UserId).Distinct().ToHashSet();
        stats.ApiEnabledUsers = apiEnabledUserIds.Count;
        stats.ApiEnabledUsersPercentage = stats.TotalUsers > 0 ? Math.Round((double)stats.ApiEnabledUsers / stats.TotalUsers * 100, 1) : 0;

        return stats;
    }

    private UsageStatistics CalculateUsageStatistics(List<UserAccount> allUsers)
    {
        var stats = new UsageStatistics
        {
            TotalAnalysesUsed = allUsers.Sum(u => u.AnalysesUsed),
            TotalFeedQueriesUsed = allUsers.Sum(u => u.FeedQueriesUsed),
            TotalActiveReports = allUsers.Sum(u => u.ActiveReports),
            TotalApiCalls = allUsers.Sum(u => u.ApiUsed)
        };

        // Calculate tier usage metrics
        var tierGroups = allUsers.GroupBy(u => u.Tier).ToList();
        foreach (var group in tierGroups)
        {
            var tierName = group.Key.ToString();
            var userCount = group.Count();
            var limits = GetLimitsForTier(group.Key);
            var totalAnalysesUsed = group.Sum(u => u.AnalysesUsed);
            var totalCapacity = userCount * limits.AnalysisLimit;

            stats.TierUsageMetrics[tierName] = new TierUsageMetrics
            {
                UserCount = userCount,
                TierLimit = limits.AnalysisLimit,
                TotalUsed = totalAnalysesUsed,
                UtilizationPercentage = totalCapacity > 0 ? Math.Round((double)totalAnalysesUsed / totalCapacity * 100, 1) : 0,
                UsageType = "Analyses"
            };
        }

        // Calculate top users by combined usage
        stats.TopUsers = allUsers
            .Select(u => new TopUserUsage
            {
                UserId = u.UserId,
                MaskedUserId = MaskUserId(u.UserId),
                Tier = u.Tier.ToString(),
                CombinedUsage = u.AnalysesUsed + u.FeedQueriesUsed + u.ApiUsed,
                AnalysesUsed = u.AnalysesUsed,
                FeedQueriesUsed = u.FeedQueriesUsed,
                ApiUsed = u.ApiUsed
            })
            .OrderByDescending(u => u.CombinedUsage)
            .Take(5)
            .ToList();

        return stats;
    }

    private ApiStatistics CalculateApiStatistics(List<UserAccount> allUsers, List<ApiKey> apiKeys, DateTime last7Days)
    {
        var apiEnabledKeys = apiKeys.Where(k => k.IsEnabled).ToList();
        var apiUsers = allUsers.Where(u => apiEnabledKeys.Any(k => k.UserId == u.UserId)).ToList();
        var totalApiCalls = apiUsers.Sum(u => u.ApiUsed);

        var stats = new ApiStatistics
        {
            TotalApiEnabledUsers = apiUsers.Count,
            TotalApiCalls = totalApiCalls,
            AverageApiCallsPerUser = apiUsers.Count > 0 ? Math.Round((double)totalApiCalls / apiUsers.Count, 1) : 0,
            RecentlyActiveApiUsers = apiKeys.Count(k => k.IsEnabled && k.LastUsedAt >= last7Days)
        };

        // Calculate top API users
        stats.TopApiUsers = apiUsers
            .Where(u => u.ApiUsed > 0)
            .Select(u => new TopApiUser
            {
                UserId = u.UserId,
                MaskedUserId = MaskUserId(u.UserId),
                Tier = u.Tier.ToString(),
                ApiCalls = u.ApiUsed,
                LastUsedAt = apiKeys.FirstOrDefault(k => k.UserId == u.UserId)?.LastUsedAt
            })
            .OrderByDescending(u => u.ApiCalls)
            .Take(5)
            .ToList();

        return stats;
    }

    private static string MaskUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "***";
        if (userId.Length <= 6) return new string('*', userId.Length);
        
        return userId[..3] + new string('*', Math.Max(1, userId.Length - 6)) + userId[^3..];
    }

    #endregion
}
