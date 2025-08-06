using System;
using Azure.Data.Tables;

namespace SharedDump.Models.Account;

public class ApiKeyEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "apikeys";
    public string RowKey { get; set; } = string.Empty; // This will be the API key
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public string Name { get; set; } = "API Key";

    public ApiKeyEntity() { }

    public ApiKeyEntity(ApiKey apiKey)
    {
        RowKey = apiKey.Key;
        UserId = apiKey.UserId;
        IsEnabled = apiKey.IsEnabled;
        CreatedAt = apiKey.CreatedAt;
        LastUsedAt = apiKey.LastUsedAt;
        Name = apiKey.Name;
    }

    public ApiKey ToApiKey()
    {
        return new ApiKey
        {
            Key = RowKey,
            UserId = UserId,
            IsEnabled = IsEnabled,
            CreatedAt = CreatedAt,
            LastUsedAt = LastUsedAt,
            Name = Name
        };
    }
}