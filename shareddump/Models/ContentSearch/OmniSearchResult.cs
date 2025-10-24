namespace SharedDump.Models.ContentSearch;

/// <summary>
/// Lightweight DTO for omni-search results across multiple platforms
/// </summary>
public class OmniSearchResult
{
    /// <summary>
    /// Unique identifier for the result (platform-specific ID)
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Title of the content
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Brief snippet/description of the content
    /// </summary>
    public string? Snippet { get; set; }

    /// <summary>
    /// Source platform (YouTube, Reddit, HackerNews, Twitter, BlueSky)
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Platform-specific source identifier (video ID, post ID, etc.)
    /// </summary>
    public required string SourceId { get; set; }

    /// <summary>
    /// Direct URL to the content
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// When the content was published
    /// </summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// Author or channel name
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Engagement metric (views, likes, upvotes, etc.)
    /// </summary>
    public long EngagementCount { get; set; }

    /// <summary>
    /// Number of comments/replies on the content
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Optional: raw platform-specific payload for future expansion
    /// </summary>
    public object? RawPayload { get; set; }
}

/// <summary>
/// Request model for omni-search queries
/// </summary>
public class OmniSearchRequest
{
    /// <summary>
    /// Search query string
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// List of platforms to search (youtube, reddit, hackernews, twitter, bluesky)
    /// </summary>
    public required List<string> Platforms { get; set; }

    /// <summary>
    /// Optional: start of date range filter
    /// </summary>
    public DateTimeOffset? FromDate { get; set; }

    /// <summary>
    /// Optional: end of date range filter
    /// </summary>
    public DateTimeOffset? ToDate { get; set; }

    /// <summary>
    /// Maximum results per platform (default: 10)
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Sort mode: "chronological" (default) or "ranked" (engagement + recency)
    /// </summary>
    public string SortMode { get; set; } = "chronological";

    /// <summary>
    /// Hide results with zero comments (default: false)
    /// </summary>
    public bool HideZeroComments { get; set; } = false;

    /// <summary>
    /// Page number for pagination (1-indexed)
    /// </summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// Response model for omni-search results
/// </summary>
public class OmniSearchResponse
{
    /// <summary>
    /// List of search results
    /// </summary>
    public required List<OmniSearchResult> Results { get; set; }

    /// <summary>
    /// Total result count across all platforms
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Results per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Cache timestamp (when results were generated)
    /// </summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Query that was executed
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Platforms that were searched
    /// </summary>
    public required List<string> PlatformsSearched { get; set; }
}
