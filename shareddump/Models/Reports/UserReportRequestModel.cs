using System;
using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models.Reports;

/// <summary>
/// Represents a user-specific report request stored in Azure Table Storage
/// PartitionKey: UserId
/// RowKey: Unique identifier for the request (combination of type and source details)
/// </summary>
public class UserReportRequestModel : ITableEntity
{
    /// <summary>
    /// Unique identifier for this user request (generated from type and source details)
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// User ID who made this request
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of request: "reddit" or "github"
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Subreddit name (if type is "reddit")
    /// </summary>
    public string? Subreddit { get; set; }
    
    /// <summary>
    /// GitHub owner name (if type is "github")
    /// </summary>
    public string? Owner { get; set; }
    
    /// <summary>
    /// GitHub repository name (if type is "github")
    /// </summary>
    public string? Repo { get; set; }
    
    /// <summary>
    /// When this request was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Display name for the request (auto-generated)
    /// </summary>
    public string DisplayName => Type == "reddit" ? $"r/{Subreddit}" : $"{Owner}/{Repo}";

    // ITableEntity properties
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey { get; set; } = string.Empty; // Id
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
