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

    /// <summary>
    /// Get the user's API key if one exists
    /// </summary>
    /// <returns>The user's API key, or null if none exists or failed to load</returns>
    Task<ApiKey?> GetApiKeyAsync();

    /// <summary>
    /// Create a new API key for the user
    /// </summary>
    /// <param name="name">Optional name for the API key</param>
    /// <returns>The created API key, or null if failed to create</returns>
    Task<ApiKey?> CreateApiKeyAsync(string? name = null);

    /// <summary>
    /// Delete the user's API key
    /// </summary>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteApiKeyAsync();
}
