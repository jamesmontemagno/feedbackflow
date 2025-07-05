using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SharedDump.Models.Account;
using SharedDump.Services.Account;
using SharedDump.Utils.Account;

namespace FeedbackFunctions.Services
{
    public class AccountLimitsService : SharedDump.Services.Account.IAccountLimitsService
    {
        private readonly IUserAccountTableService _userAccountTable;
        private readonly IUsageRecordTableService _usageRecordTable;
        private readonly IConfiguration _configuration;

        public AccountLimitsService(IUserAccountTableService userAccountTable, IUsageRecordTableService usageRecordTable, IConfiguration configuration)
        {
            _userAccountTable = userAccountTable;
            _usageRecordTable = usageRecordTable;
            _configuration = configuration;
        }

        public AccountLimits GetLimitsForTier(AccountTier tier)
        {
            return tier switch
            {
                AccountTier.Free => new AccountLimits 
                { 
                    AnalysisLimit = _configuration.GetValue<int>("FREE_TIER_ANALYSIS_LIMIT", 10),
                    ReportLimit = _configuration.GetValue<int>("FREE_TIER_REPORT_LIMIT", 1),
                    FeedQueryLimit = _configuration.GetValue<int>("FREE_TIER_FEED_QUERY_LIMIT", 20)
                },
                AccountTier.Pro => new AccountLimits 
                { 
                    AnalysisLimit = _configuration.GetValue<int>("PRO_TIER_ANALYSIS_LIMIT", 75),
                    ReportLimit = _configuration.GetValue<int>("PRO_TIER_REPORT_LIMIT", 5),
                    FeedQueryLimit = _configuration.GetValue<int>("PRO_TIER_FEED_QUERY_LIMIT", 200)
                },
                AccountTier.ProPlus => new AccountLimits 
                { 
                    AnalysisLimit = _configuration.GetValue<int>("PROPLUS_TIER_ANALYSIS_LIMIT", 300),
                    ReportLimit = _configuration.GetValue<int>("PROPLUS_TIER_REPORT_LIMIT", 25),
                    FeedQueryLimit = _configuration.GetValue<int>("PROPLUS_TIER_FEED_QUERY_LIMIT", 1000)
                },
                _ => new AccountLimits { AnalysisLimit = 0, ReportLimit = 0, FeedQueryLimit = 0 }
            };
        }

        public bool IsWithinLimits(string userId, UsageType usageType)
        {
            // TODO: Implement actual lookup
            return true;
        }

        public async Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType)
        {
            var user = await _userAccountTable.GetUserAccountAsync(userId);
            if (user == null)
            {
                return new UsageValidationResult { IsWithinLimit = false, UsageType = usageType };
            }
            
            // Get limits for the user's tier
            var limits = GetLimitsForTier((AccountTier)user.Tier);
            
            // Check usage against tier limits
            bool withinLimit = usageType switch
            {
                UsageType.Analysis => user.AnalysesUsed < limits.AnalysisLimit,
                UsageType.FeedQuery => user.FeedQueriesUsed < limits.FeedQueryLimit,
                UsageType.ReportCreated => user.ActiveReports < limits.ReportLimit,
                _ => true
            };
            return new UsageValidationResult { IsWithinLimit = withinLimit, UsageType = usageType };
        }

        public async Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null)
        {
            var user = await _userAccountTable.GetUserAccountAsync(userId);
            if (user == null) return;
            
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
            
            // Simple usage record for audit (no detailed tracking)
            await _usageRecordTable.AddUsageRecordAsync(new UsageRecordEntity
            {
                PartitionKey = userId,
                RowKey = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{usageType}_{resourceId ?? "unknown"}",
                UsageType = (int)usageType,
                ResourceId = resourceId ?? string.Empty,
                Details = string.Empty, // Keep simple
                UsageTimestamp = DateTime.UtcNow
            });
        }
    }
}
