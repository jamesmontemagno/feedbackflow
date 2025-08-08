namespace FeedbackWebApp.Services.Account;

/// <summary>
/// Mock implementation of admin API key service for development/testing
/// </summary>
public class MockAdminApiKeyService : IAdminApiKeyService
{
    private readonly ILogger<MockAdminApiKeyService> _logger;
    
    // Simulate some API key data
    private static readonly List<AdminApiKeyInfo> _mockApiKeys = new()
    {
        new AdminApiKeyInfo
        {
            Key = "ff_abcd1234...5678",
            FullKey = "ff_abcd1234567890abcdef1234567890abcdef5678",
            UserId = "user****1234",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            LastUsedAt = DateTime.UtcNow.AddHours(-2),
            Name = "Production API Key"
        },
        new AdminApiKeyInfo
        {
            Key = "ff_efgh5678...9012",
            FullKey = "ff_efgh567890123456fedcba0987654321fedcba9012",
            UserId = "user****5678",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastUsedAt = DateTime.UtcNow.AddDays(-5),
            Name = "Development Key"
        },
        new AdminApiKeyInfo
        {
            Key = "ff_ijkl9012...3456",
            FullKey = "ff_ijkl901234567890abcdef1234567890abcdef3456",
            UserId = "user****9012",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastUsedAt = DateTime.UtcNow.AddMinutes(-30),
            Name = "Testing API Key"
        },
        new AdminApiKeyInfo
        {
            Key = "ff_mnop3456...7890",
            FullKey = "ff_mnop345678901234fedcba0987654321fedcba7890",
            UserId = "user****3456",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            LastUsedAt = null,
            Name = "Inactive Key"
        }
    };

    public MockAdminApiKeyService(ILogger<MockAdminApiKeyService> logger)
    {
        _logger = logger;
    }

    public async Task<List<AdminApiKeyInfo>?> GetAllApiKeysAsync()
    {
        await Task.Delay(200); // Simulate network delay
        _logger.LogInformation("Mock: Getting all API keys - returning {Count} keys", _mockApiKeys.Count);
        
        // Return a copy to simulate fresh data from API
        return _mockApiKeys.Select(k => new AdminApiKeyInfo
        {
            Key = k.Key,
            FullKey = k.FullKey,
            UserId = k.UserId,
            IsEnabled = k.IsEnabled,
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt,
            Name = k.Name
        }).ToList();
    }

    public async Task<bool> UpdateApiKeyStatusAsync(string apiKey, bool isEnabled)
    {
        await Task.Delay(100); // Simulate network delay
        
        // Find the API key by its full key
        var keyToUpdate = _mockApiKeys.FirstOrDefault(k => k.FullKey == apiKey);
        if (keyToUpdate != null)
        {
            keyToUpdate.IsEnabled = isEnabled;
            _logger.LogInformation("Mock: Updated API key {Key} status to {IsEnabled}", 
                keyToUpdate.Key, isEnabled);
            return true;
        }
        
        _logger.LogWarning("Mock: API key {Key} not found", apiKey);
        return false;
    }
}