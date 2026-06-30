using SharedDump.Models.Reddit;

namespace SharedDump.Models.Reports;

/// <summary>
/// Raw, unprocessed Reddit data captured at the time a Reddit report was generated.
/// Stored as a single JSON blob (keyed by <see cref="ReportId"/>) so the underlying
/// threads and comments that fed a report can be inspected or downloaded later.
/// Contains the fetched data only (threads, comments, subreddit info) - no AI analyses.
/// </summary>
public class RedditReportRawData
{
    /// <summary>
    /// Identifier of the <see cref="ReportModel"/> this raw data belongs to.
    /// </summary>
    public Guid ReportId { get; set; }

    /// <summary>
    /// The subreddit the report was generated for (without the "r/" prefix).
    /// </summary>
    public string Subreddit { get; set; } = string.Empty;

    /// <summary>
    /// When the report (and this raw data) was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The cutoff date used when fetching threads for the report.
    /// </summary>
    public DateTimeOffset CutoffDate { get; set; }

    /// <summary>
    /// Number of threads captured in this raw data set.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Total number of comments captured across all threads.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Metadata about the subreddit at the time of capture.
    /// </summary>
    public RedditSubredditInfo? SubredditInfo { get; set; }

    /// <summary>
    /// The full threads (including nested comments) captured from the last week.
    /// Includes every thread from the report's time window, not just the analyzed top threads.
    /// </summary>
    public List<RedditThreadModel> Threads { get; set; } = new();
}
