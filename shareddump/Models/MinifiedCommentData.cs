using System.Text.Json.Serialization;

namespace SharedDump.Models;

/// <summary>
/// Minified version of CommentData for efficient data transfer to AI analysis backend.
/// Contains only essential fields needed for sentiment and feedback analysis.
/// </summary>
public record MinifiedCommentData
{
    /// <summary>
    /// Author of the comment (required for analysis)
    /// </summary>
    [JsonPropertyName("a")]
    public string Author { get; init; } = string.Empty;
    
    /// <summary>
    /// Comment content/text (required for analysis)
    /// </summary>
    [JsonPropertyName("c")]
    public string Content { get; init; } = string.Empty;
    
    /// <summary>
    /// When the comment was created (useful for temporal analysis)
    /// </summary>
    [JsonPropertyName("t")]
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Score/upvotes/likes if available (useful for weighting feedback)
    /// </summary>
    [JsonPropertyName("s")]
    public int? Score { get; init; }
    
    /// <summary>
    /// Nested replies to this comment
    /// </summary>
    [JsonPropertyName("r")]
    public List<MinifiedCommentData> Replies { get; init; } = new();
}

/// <summary>
/// Minified version of CommentThread for efficient data transfer to AI analysis backend.
/// Contains only essential fields needed for sentiment and feedback analysis.
/// </summary>
public record MinifiedCommentThread
{
    /// <summary>
    /// Title of the thread/post/video
    /// </summary>
    [JsonPropertyName("t")]
    public string Title { get; init; } = string.Empty;
    
    /// <summary>
    /// Description or body content of the original post
    /// </summary>
    [JsonPropertyName("d")]
    public string? Description { get; init; }
    
    /// <summary>
    /// Author of the original post
    /// </summary>
    [JsonPropertyName("a")]
    public string Author { get; init; } = string.Empty;
    
    /// <summary>
    /// When the thread was created
    /// </summary>
    [JsonPropertyName("c")]
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Platform identifier (YouTube, Reddit, GitHub, etc.)
    /// </summary>
    [JsonPropertyName("p")]
    public string Platform { get; init; } = string.Empty;
    
    /// <summary>
    /// All comments in the thread
    /// </summary>
    [JsonPropertyName("co")]
    public List<MinifiedCommentData> Comments { get; init; } = new();
}
