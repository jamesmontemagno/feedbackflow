using System.Threading.Tasks;
using SharedDump.Models.Account;
using SharedDump.Services.Account;

namespace SharedDump.Services.Account
{
    /// <summary>
    /// Simple usage tracking service for web app contexts
    /// </summary>
    public class WebAppUsageTrackingService : IUsageTrackingService
    {
        private readonly IAccountLimitsService _limitsService;
        private readonly IUserAccountTableService _userAccountTable;
        private const string DefaultUserId = "demo-user"; // Fallback for web app contexts

        public WebAppUsageTrackingService(IAccountLimitsService limitsService, IUserAccountTableService userAccountTable)
        {
            _limitsService = limitsService;
            _userAccountTable = userAccountTable;
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
                // Create a default user account without limits (they'll be calculated dynamically)
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
                    ActiveReports = 0
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
                ActiveReports = userEntity.ActiveReports
            };
        }

        public async Task<AccountLimits> GetUserLimitsAsync()
        {
            var userEntity = await _userAccountTable.GetUserAccountAsync(DefaultUserId);
            var tier = userEntity != null ? (AccountTier)userEntity.Tier : AccountTier.Free;
            return _limitsService.GetLimitsForTier(tier);
        }

        public async Task RefreshUsageLimitsAsync()
        {
            // Limits are now calculated dynamically based on tier
            // No need to update stored limits as they don't exist anymore
            await Task.CompletedTask;
        }
    }
}
