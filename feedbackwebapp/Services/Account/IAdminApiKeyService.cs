namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Frontend interface for admin API key management - calls backend APIs
/// </summary>
public interface IAdminApiKeyService
{
    /// <summary>
    /// Get all API keys (admin only)
    /// </summary>
    /// <returns>List of API keys with masked user IDs, or null if failed to load</returns>
    Task<List<AdminApiKeyInfo>?> GetAllApiKeysAsync();

    /// <summary>
    /// Update API key status (admin only)
    /// </summary>
    /// <param name="apiKey">The API key to update</param>
    /// <param name="isEnabled">New enabled status</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateApiKeyStatusAsync(string apiKey, bool isEnabled);
}

/// <summary>
/// API key information for admin view with masked user ID
/// </summary>
public class AdminApiKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public string FullKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string Name { get; set; } = string.Empty;
}