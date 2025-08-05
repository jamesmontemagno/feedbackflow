using System.Security.Cryptography;
using System.Text;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Account;

namespace FeedbackFunctions.Services.Account;

public class ApiKeyService : IApiKeyService
{
    private readonly ILogger<ApiKeyService> _logger;
    private readonly TableClient _tableClient;
    private const string TableName = "apikeys";

    public ApiKeyService(ILogger<ApiKeyService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var connectionString = configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
        var tableServiceClient = new TableServiceClient(connectionString);
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<ApiKey?> GetApiKeyByUserIdAsync(string userId)
    {
        try
        {
            var filter = $"UserId eq '{userId}'";
            await foreach (var entity in _tableClient.QueryAsync<ApiKeyEntity>(filter))
            {
                return entity?.ToApiKey();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key for user {UserId}", userId);
            return null;
        }
    }

    public async Task<ApiKey?> GetApiKeyByKeyAsync(string apiKey)
    {
        try
        {
            var response = await _tableClient.GetEntityIfExistsAsync<ApiKeyEntity>("apikeys", apiKey);
            return response.HasValue ? response.Value.ToApiKey() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key by key");
            return null;
        }
    }

    public async Task<ApiKey> CreateApiKeyAsync(string userId, string? name = null)
    {
        try
        {
            // Check if user already has an API key
            var existingKey = await GetApiKeyByUserIdAsync(userId);
            if (existingKey != null)
            {
                throw new InvalidOperationException("User already has an API key. Delete the existing key first.");
            }

            // Generate a new API key
            var apiKey = GenerateApiKey();
            
            var newApiKey = new ApiKey
            {
                Key = apiKey,
                UserId = userId,
                IsEnabled = false, // Disabled by default
                CreatedAt = DateTime.UtcNow,
                Name = name ?? "API Key"
            };

            var entity = new ApiKeyEntity(newApiKey);
            await _tableClient.AddEntityAsync(entity);

            _logger.LogInformation("Created new API key for user {UserId}", userId);
            return newApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> DeleteApiKeyAsync(string userId)
    {
        try
        {
            var existingKey = await GetApiKeyByUserIdAsync(userId);
            if (existingKey == null)
            {
                return false;
            }

            await _tableClient.DeleteEntityAsync("apikeys", existingKey.Key);
            _logger.LogInformation("Deleted API key for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API key for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            var key = await GetApiKeyByKeyAsync(apiKey);
            return key != null && key.IsEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return false;
        }
    }

    public async Task UpdateLastUsedAsync(string apiKey)
    {
        try
        {
            var entity = await _tableClient.GetEntityIfExistsAsync<ApiKeyEntity>("apikeys", apiKey);
            if (entity.HasValue)
            {
                var apiKeyEntity = entity.Value;
                apiKeyEntity.LastUsedAt = DateTime.UtcNow;
                await _tableClient.UpdateEntityAsync(apiKeyEntity, apiKeyEntity.ETag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last used timestamp for API key");
        }
    }

    private static string GenerateApiKey()
    {
        // Generate a secure random API key
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        // Convert to base64 and make it URL-safe
        var apiKey = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
            
        return $"ff_{apiKey}"; // Add prefix to identify FeedbackFlow API keys
    }
}