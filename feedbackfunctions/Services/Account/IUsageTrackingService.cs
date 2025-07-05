using System.Threading.Tasks;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Services.Account;

public interface IUsageTrackingService
{
    Task<bool> CanPerformActionAsync(UsageType usageType);
    Task TrackActionAsync(UsageType usageType, string? resourceId = null);
    Task<UserAccount> GetUserAccountAsync();
    Task<AccountLimits> GetUserLimitsAsync();
    Task RefreshUsageLimitsAsync();
}
