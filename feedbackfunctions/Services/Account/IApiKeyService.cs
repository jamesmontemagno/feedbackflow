using SharedDump.Models.Account;

namespace FeedbackFunctions.Services.Account;

public interface IApiKeyService
{
    Task<ApiKey?> GetApiKeyByUserIdAsync(string userId);
    Task<ApiKey?> GetApiKeyByKeyAsync(string apiKey);
    Task<ApiKey> CreateApiKeyAsync(string userId, string? name = null);
    Task<bool> DeleteApiKeyAsync(string userId);
    Task<bool> ValidateApiKeyAsync(string apiKey);
    Task UpdateLastUsedAsync(string apiKey);
}