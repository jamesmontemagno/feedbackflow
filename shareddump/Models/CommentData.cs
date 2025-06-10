using System.Text.Json.Serialization;

namespace SharedDump.Models;

/// <summary>
/// Represents a single comment with support for nested replies across all platforms
/// </summary>
public record CommentData
{
    /// <summary>
    /// Unique identifier for the comment
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// ID of the parent comment (null for top-level comments)
    /// </summary>
    public string? ParentId { get; init; }
    
    /// <summary>
    /// Author of the comment
    /// </summary>
    public string Author { get; init; } = string.Empty;
    
    /// <summary>
    /// Comment content/text
    /// </summary>
    public string Content { get; init; } = string.Empty;
    
    /// <summary>
    /// When the comment was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Direct URL to the comment (if available)
    /// </summary>
    public string? Url { get; init; }
    
    /// <summary>
    /// Score/upvotes/likes (if available)
    /// </summary>
    public int? Score { get; init; }
    
    /// <summary>
    /// Platform-specific metadata (e.g., like count, view count, etc.)
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// Nested replies to this comment
    /// </summary>
    public List<CommentData> Replies { get; init; } = new();
}

/// <summary>
/// Represents a thread/post/video with its associated comments
/// </summary>
public record CommentThread
{
    /// <summary>
    /// Unique identifier for the thread
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Title of the thread/post/video
    /// </summary>
    public string Title { get; init; } = string.Empty;
    
    /// <summary>
    /// Description or body content of the original post
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Author of the original post
    /// </summary>
    public string Author { get; init; } = string.Empty;
    
    /// <summary>
    /// When the thread was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// URL to the original post/thread
    /// </summary>
    public string? Url { get; init; }
    
    /// <summary>
    /// Platform identifier (YouTube, Reddit, GitHub, etc.)
    /// </summary>
    public string SourceType { get; init; } = string.Empty;
    
    /// <summary>
    /// Platform-specific metadata (e.g., view count, subscriber count, etc.)
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// All comments in the thread
    /// </summary>
    public List<CommentData> Comments { get; init; } = new();
}