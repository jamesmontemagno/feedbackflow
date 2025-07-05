using System.Threading.Tasks;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Services.Account;

public interface IAccountLimitsService
{
    AccountLimits GetLimitsForTier(AccountTier tier);
    bool IsWithinLimits(string userId, UsageType usageType);
    Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType);
    Task TrackUsageAsync(string userId, UsageType usageType, string? resourceId = null);
}
