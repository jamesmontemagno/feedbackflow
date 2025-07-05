using System.Threading.Tasks;

using SharedDump.Models.Account;
namespace SharedDump.Services.Account
{
    public interface IUsageTrackingService
    {
        Task<bool> CanPerformActionAsync(UsageType usageType);
        Task TrackActionAsync(UsageType usageType, string? resourceId = null);
        Task<UserAccount> GetUserAccountAsync();
        Task RefreshUsageLimitsAsync();
    }
}
