using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models.Authentication;

/// <summary>
/// Represents a user authenticated through various providers, stored in Azure Table Storage
/// </summary>
public class AuthUserEntity : ITableEntity
{
    /// <summary>
    /// Partition key - AuthProvider (e.g., "Microsoft", "Google", "GitHub")
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row key - ProviderUserId (unique ID from the provider)
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
    /// Internal unique user ID (GUID)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred email address for notifications (independent of auth provider email)
    /// </summary>
    public string? PreferredEmail { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Authentication provider name (redundant for queries)
    /// </summary>
    public string AuthProvider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific user ID (redundant for queries)
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// When the user was first created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Whether the user is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// URL to the user's profile image
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// JSON data for provider-specific information
    /// </summary>
    public string? ProviderData { get; set; }

    /// <summary>
    /// Default constructor for Table Storage
    /// </summary>
    public AuthUserEntity() { }

    /// <summary>
    /// Constructor for creating a new user
    /// </summary>
    /// <param name="provider">Authentication provider name</param>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <param name="email">User's email address</param>
    /// <param name="name">User's display name</param>
    public AuthUserEntity(string provider, string providerUserId, string email, string name)
    {
        PartitionKey = provider;
        RowKey = providerUserId;
        AuthProvider = provider;
        ProviderUserId = providerUserId;
        UserId = Guid.NewGuid().ToString();
        Email = email;
        Name = name;
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
}