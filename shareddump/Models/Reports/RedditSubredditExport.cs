using SharedDump.Models.Reddit;

namespace SharedDump.Models.Reports;

/// <summary>
/// A complete, on-demand export of a subreddit's threads and comments captured for a
/// specific date/time window. Stored as a single JSON blob (keyed by <see cref="Id"/>)
/// in the admin Reddit export portal. Contains the fetched data only (threads, comments,
/// subreddit info) - no AI analyses.
/// </summary>
public class RedditSubredditExport
{
    /// <summary>
    /// Unique identifier for this export (also the blob name, without extension).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The subreddit the export was generated for (without the "r/" prefix).
    /// </summary>
    public string Subreddit { get; set; } = string.Empty;

    /// <summary>
    /// Start of the capture window (inclusive, UTC).
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End of the capture window (inclusive, UTC).
    /// </summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// When this export was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of threads captured in this export.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Total number of comments captured across all threads.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Whether the export hit the configured maximum thread cap (i.e. the window may contain
    /// more threads than were captured).
    /// </summary>
    public bool ThreadLimitReached { get; set; }

    /// <summary>
    /// Metadata about the subreddit at the time of capture.
    /// </summary>
    public RedditSubredditInfo? SubredditInfo { get; set; }

    /// <summary>
    /// The full threads (including nested comments) captured in the window.
    /// </summary>
    public List<RedditThreadModel> Threads { get; set; } = new();
}

/// <summary>
/// Request payload to create a new Reddit subreddit export.
/// </summary>
public class CreateRedditExportRequest
{
    /// <summary>
    /// Subreddit name (without the "r/" prefix).
    /// </summary>
    public string Subreddit { get; set; } = string.Empty;

    /// <summary>
    /// Start of the capture window (inclusive, UTC).
    /// </summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>
    /// End of the capture window (inclusive, UTC).
    /// </summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>
    /// Maximum number of threads to capture (server clamps this to a safe range).
    /// </summary>
    public int MaxThreads { get; set; } = 200;
}

/// <summary>
/// Lightweight metadata describing a stored Reddit export for the admin export portal.
/// </summary>
public class RedditExportListItem
{
    public Guid Id { get; set; }
    public string Subreddit { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public int ThreadCount { get; set; }
    public int CommentCount { get; set; }
    public bool ThreadLimitReached { get; set; }
}

/// <summary>
/// Response payload for the admin Reddit export listing endpoint.
/// </summary>
public class RedditExportListResponse
{
    public List<RedditExportListItem> Exports { get; set; } = new();
}
