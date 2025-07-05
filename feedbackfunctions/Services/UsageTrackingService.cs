using System.Threading.Tasks;
using SharedDump.Models.Account;
using SharedDump.Services.Account;
using FeedbackFunctions.Services.Authentication;

namespace FeedbackFunctions.Services
{
    public class UsageTrackingService : SharedDump.Services.Account.IUsageTrackingService
    {
        private readonly IAccountLimitsService _limitsService;
        private readonly IUserAccountTableService _userAccountTable;
        private readonly AuthenticationMiddleware _authMiddleware;
        private const string DefaultUserId = "demo-user"; // Fallback for non-authenticated contexts

        public UsageTrackingService(IAccountLimitsService limitsService, IUserAccountTableService userAccountTable, AuthenticationMiddleware authMiddleware)
        {
            _limitsService = limitsService;
            _userAccountTable = userAccountTable;
            _authMiddleware = authMiddleware;
        }

        public async Task<bool> CanPerformActionAsync(UsageType usageType)
        {
            var result = await _limitsService.ValidateUsageAsync(DefaultUserId, usageType);
            return result.IsWithinLimit;
        }

        public async Task TrackActionAsync(UsageType usageType, string? resourceId = null)
        {
            await _limitsService.TrackUsageAsync(DefaultUserId, usageType, resourceId);
        }

        public async Task<UserAccount> GetUserAccountAsync()
        {
            var userEntity = await _userAccountTable.GetUserAccountAsync(DefaultUserId);
            if (userEntity == null)
            {
                // Create a default user account
                var limits = _limitsService.GetLimitsForTier(AccountTier.Free);
                var defaultUser = new UserAccountEntity
                {
                    PartitionKey = DefaultUserId,
                    RowKey = "account",
                    Tier = (int)AccountTier.Free,
                    SubscriptionStart = System.DateTime.UtcNow,
                    IsActive = true,
                    CreatedAt = System.DateTime.UtcNow,
                    LastResetDate = System.DateTime.UtcNow,
                    AnalysesUsed = 0,
                    FeedQueriesUsed = 0,
                    ActiveReports = 0,
                    AnalysisLimit = limits.AnalysisLimit,
                    ReportLimit = limits.ReportLimit,
                    FeedQueryLimit = limits.FeedQueryLimit
                };
                await _userAccountTable.UpsertUserAccountAsync(defaultUser);
                userEntity = defaultUser;
            }

            return new UserAccount
            {
                UserId = userEntity.PartitionKey,
                Tier = (AccountTier)userEntity.Tier,
                SubscriptionStart = userEntity.SubscriptionStart,
                SubscriptionEnd = userEntity.SubscriptionEnd,
                IsActive = userEntity.IsActive,
                CreatedAt = userEntity.CreatedAt,
                LastResetDate = userEntity.LastResetDate,
                AnalysesUsed = userEntity.AnalysesUsed,
                FeedQueriesUsed = userEntity.FeedQueriesUsed,
                ActiveReports = userEntity.ActiveReports,
                AnalysisLimit = userEntity.AnalysisLimit,
                ReportLimit = userEntity.ReportLimit,
                FeedQueryLimit = userEntity.FeedQueryLimit
            };
        }

        public async Task RefreshUsageLimitsAsync()
        {
            var userEntity = await _userAccountTable.GetUserAccountAsync(DefaultUserId);
            if (userEntity != null)
            {
                var limits = _limitsService.GetLimitsForTier((AccountTier)userEntity.Tier);
                userEntity.AnalysisLimit = limits.AnalysisLimit;
                userEntity.ReportLimit = limits.ReportLimit;
                userEntity.FeedQueryLimit = limits.FeedQueryLimit;
                await _userAccountTable.UpsertUserAccountAsync(userEntity);
            }
        }
    }
}
