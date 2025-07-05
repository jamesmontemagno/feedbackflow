using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SharedDump.Models.Account;
using SharedDump.Services.Account;

namespace SharedDump.Services.Account
{
    public class AccountLimitsService : IAccountLimitsService
    {
        private readonly IUserAccountTableService _userAccountTable;
        private readonly IUsageRecordTableService _usageRecordTable;
        private readonly IConfiguration? _configuration;

        public AccountLimitsService(IUserAccountTableService userAccountTable, IUsageRecordTableService usageRecordTable, IConfiguration? configuration = null)
        {
            _userAccountTable = userAccountTable;
            _usageRecordTable = usageRecordTable;
            _configuration = configuration;
        }

        public AccountLimits GetLimitsForTier(AccountTier tier)
        {
            // Use environment variables if configuration is available, otherwise use defaults
            return tier switch
            {
                AccountTier.Free => new AccountLimits 
                { 
                    AnalysisLimit = GetConfigValue("AccountTiers:Free:AnalysisLimit", 10),
                    ReportLimit = GetConfigValue("AccountTiers:Free:ReportLimit", 1),
                    FeedQueryLimit = GetConfigValue("AccountTiers:Free:FeedQueryLimit", 20)
                },
                AccountTier.Pro => new AccountLimits 
                { 
                    AnalysisLimit = GetConfigValue("AccountTiers:Pro:AnalysisLimit", 75),
                    ReportLimit = GetConfigValue("AccountTiers:Pro:ReportLimit", 5),
                    FeedQueryLimit = GetConfigValue("AccountTiers:Pro:FeedQueryLimit", 200)
                },
                AccountTier.ProPlus => new AccountLimits 
                { 
                    AnalysisLimit = GetConfigValue("AccountTiers:ProPlus:AnalysisLimit", 300),
                    ReportLimit = GetConfigValue("AccountTiers:ProPlus:ReportLimit", 25),
                    FeedQueryLimit = GetConfigValue("AccountTiers:ProPlus:FeedQueryLimit", 1000)
                },
                _ => new AccountLimits { AnalysisLimit = 0, ReportLimit = 0, FeedQueryLimit = 0 }
            };
        }

        private int GetConfigValue(string key, int defaultValue)
        {
            if (_configuration == null) return defaultValue;
            
            var value = _configuration[key];
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        public bool IsWithinLimits(string userId, UsageType usageType)
        {
            var result = ValidateUsageAsync(userId, usageType).GetAwaiter().GetResult();
            return result.IsWithinLimit;
        }

        public async Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType)
        {
            var user = await _userAccountTable.GetUserAccountAsync(userId);
            if (user == null)
            {
                return new UsageValidationResult 
                { 
                    IsWithinLimit = false, 
                    UsageType = usageType,
                    ErrorMessage = "User account not found",
                    CurrentTier = AccountTier.Free
                };
            }

            var limits = GetLimitsForTier((AccountTier)user.Tier);
            bool withinLimit = usageType switch
            {
                UsageType.Analysis => user.AnalysesUsed < user.AnalysisLimit,
                UsageType.FeedQuery => user.FeedQueriesUsed < user.FeedQueryLimit,
                UsageType.ReportCreated => user.ActiveReports < user.ReportLimit,
                _ => true
            };

            return new UsageValidationResult 
            { 
                IsWithinLimit = withinLimit, 
                UsageType = usageType,
                CurrentUsage = GetCurrentUsage(user, usageType),
                Limit = GetLimitForUsageType(limits, usageType),
                CurrentTier = (AccountTier)user.Tier,
                ResetDate = user.LastResetDate.AddDays(30),
                ErrorMessage = withinLimit ? null : $"Usage limit exceeded for {usageType}",
                UpgradeUrl = (AccountTier)user.Tier == AccountTier.Free ? "/upgrade" : null
            };
        }

        public async Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null)
        {
            var user = await _userAccountTable.GetUserAccountAsync(userId);
            if (user == null) 
            {
                // Create default user if not exists
                var defaultLimits = GetLimitsForTier(AccountTier.Free);
                user = new UserAccountEntity
                {
                    PartitionKey = userId,
                    RowKey = "account",
                    Tier = (int)AccountTier.Free,
                    SubscriptionStart = DateTime.UtcNow,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastResetDate = DateTime.UtcNow,
                    AnalysesUsed = 0,
                    FeedQueriesUsed = 0,
                    ActiveReports = 0,
                    AnalysisLimit = defaultLimits.AnalysisLimit,
                    ReportLimit = defaultLimits.ReportLimit,
                    FeedQueryLimit = defaultLimits.FeedQueryLimit
                };
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

            await _userAccountTable.UpsertUserAccountAsync(user);

            // Simple usage record for audit purposes (no detailed tracking)
            var usageRecord = new UsageRecordEntity
            {
                PartitionKey = userId,
                RowKey = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{usageType}",
                UsageType = (int)usageType,
                ResourceId = resourceId ?? string.Empty,
                Details = string.Empty, // Keep simple - no detailed tracking
                UsageTimestamp = DateTime.UtcNow
            };

            await _usageRecordTable.AddUsageRecordAsync(usageRecord);
        }

        private static int GetCurrentUsage(UserAccountEntity user, UsageType usageType)
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
    }
}
