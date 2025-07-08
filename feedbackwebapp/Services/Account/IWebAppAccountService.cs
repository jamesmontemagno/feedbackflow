using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Frontend interface for account management - calls backend APIs
/// </summary>
public interface IWebAppAccountService
{
    /// <summary>
    /// Get user account information and limits from backend
    /// </summary>
    /// <param name="tierInfo">Optional pre-loaded tier information to avoid duplicate API calls</param>
    /// <returns>Tuple containing user account and account limits, or null if not found</returns>
    Task<(UserAccount? account, AccountLimits limits)?> GetUserAccountAndLimitsAsync(TierInfo[]? tierInfo = null);

    /// <summary>
    /// Get tier information and limits for all available tiers
    /// </summary>
    /// <returns>Array of tier information, or null if failed to load</returns>
    Task<TierInfo[]?> GetTierLimitsAsync();
}
