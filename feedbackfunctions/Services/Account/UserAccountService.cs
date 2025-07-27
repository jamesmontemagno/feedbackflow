using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Services.Account;

public class UserAccountService : IUserAccountService
{
    private readonly TableClient _userAccountsTable;
    private readonly TableClient _usageRecordsTable;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<UserAccountService>? _logger;

    private const string UserAccountsTableName = "UserAccounts";
    private const string UsageRecordsTableName = "UsageRecords";

    public UserAccountService(string storageConnectionString, IConfiguration? configuration = null, ILogger<UserAccountService>? logger = null)
    {
        _userAccountsTable = new TableClient(storageConnectionString, UserAccountsTableName);
        _usageRecordsTable = new TableClient(storageConnectionString, UsageRecordsTableName);
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
                AnalysisRetentionDays = GetConfigValue("AccountTiers:Free:AnalysisRetentionDays", 30)
            },
            AccountTier.Pro => new AccountLimits 
            { 
                AnalysisLimit = GetConfigValue("AccountTiers:Pro:AnalysisLimit", 75),
                ReportLimit = GetConfigValue("AccountTiers:Pro:ReportLimit", 5),
                FeedQueryLimit = GetConfigValue("AccountTiers:Pro:FeedQueryLimit", 200),
                AnalysisRetentionDays = GetConfigValue("AccountTiers:Pro:AnalysisRetentionDays", 60)
            },
            AccountTier.ProPlus => new AccountLimits 
            { 
                AnalysisLimit = GetConfigValue("AccountTiers:ProPlus:AnalysisLimit", 300),
                ReportLimit = GetConfigValue("AccountTiers:ProPlus:ReportLimit", 25),
                FeedQueryLimit = GetConfigValue("AccountTiers:ProPlus:FeedQueryLimit", 1000),
                AnalysisRetentionDays = GetConfigValue("AccountTiers:ProPlus:AnalysisRetentionDays", 90)
            },
            AccountTier.SuperUser => new AccountLimits 
            { 
                AnalysisLimit = 10000,
                ReportLimit = 10000,
                FeedQueryLimit = 10000,
                AnalysisRetentionDays = 3650 // 10 years
            },
            _ => new AccountLimits { AnalysisLimit = 0, ReportLimit = 0, FeedQueryLimit = 0, AnalysisRetentionDays = 0 }
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

    public async Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null)
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
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            LastResetDate = entity.LastResetDate,
            AnalysesUsed = entity.AnalysesUsed,
            FeedQueriesUsed = entity.FeedQueriesUsed,
            ActiveReports = entity.ActiveReports,
            PreferredEmail = entity.PreferredEmail
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
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            LastResetDate = model.LastResetDate,
            AnalysesUsed = model.AnalysesUsed,
            FeedQueriesUsed = model.FeedQueriesUsed,
            ActiveReports = model.ActiveReports,
            PreferredEmail = model.PreferredEmail,
            EmailFrequency = (int)model.EmailFrequency,
            EmailNotificationsEnabled = model.EmailNotificationsEnabled,
            LastEmailSent = model.LastEmailSent
        };
    }

    #endregion
}
