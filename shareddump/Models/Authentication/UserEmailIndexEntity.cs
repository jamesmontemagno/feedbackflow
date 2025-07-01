using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models.Authentication;

/// <summary>
/// Email index entity for fast user lookups by email address
/// </summary>
public class UserEmailIndexEntity : ITableEntity
{
    /// <summary>
    /// Partition key - First letter of email (for distribution)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row key - Email address
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the entity
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Entity tag for optimistic concurrency
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Internal unique user ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Authentication provider name
    /// </summary>
    public string AuthProvider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific user ID
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Default constructor for Table Storage
    /// </summary>
    public UserEmailIndexEntity() { }

    /// <summary>
    /// Constructor for creating a new email index entry
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="userId">Internal user ID</param>
    /// <param name="provider">Authentication provider</param>
    /// <param name="providerUserId">Provider-specific user ID</param>
    public UserEmailIndexEntity(string email, string userId, string provider, string providerUserId)
    {
        PartitionKey = !string.IsNullOrEmpty(email) ? email[0].ToString().ToUpper() : "A";
        RowKey = email?.ToLower() ?? string.Empty;
        UserId = userId;
        AuthProvider = provider;
        ProviderUserId = providerUserId;
    }
}