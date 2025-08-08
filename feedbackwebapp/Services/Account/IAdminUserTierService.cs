namespace FeedbackWebApp.Services.Account;

using SharedDump.Models.Account;

/// <summary>
/// Frontend interface for admin user tier management.
/// </summary>
public interface IAdminUserTierService
{
    /// <summary>
    /// Get all user tiers (masked user id, no PII)
    /// </summary>
    Task<List<AdminUserTierInfo>?> GetAllUserTiersAsync();

    /// <summary>
    /// Update a user's tier (allowed: Free, Pro, ProPlus)
    /// </summary>
    Task<bool> UpdateUserTierAsync(string userId, AccountTier newTier);
}

public class AdminUserTierInfo
{
    public string UserId { get; set; } = string.Empty; // full id for updates
    public string MaskedUserId { get; set; } = string.Empty; // display only
    public AccountTier Tier { get; set; }
    public int AnalysesUsed { get; set; }
    public int FeedQueriesUsed { get; set; }
    public int ActiveReports { get; set; }
    public int ApiUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
