using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

/// <summary>
/// Web application service for the admin Reddit export portal. Creates on-demand subreddit
/// exports (all threads + comments within a date range), lists stored exports, and downloads
/// or deletes them via the Azure Functions backend.
/// </summary>
public interface IRedditExportService
{
    /// <summary>
    /// Creates a new subreddit export for the given window and returns its metadata.
    /// </summary>
    Task<RedditExportListItem> CreateExportAsync(CreateRedditExportRequest request);

    /// <summary>
    /// Gets metadata for all stored subreddit exports, newest first.
    /// </summary>
    Task<List<RedditExportListItem>> GetExportsAsync();

    /// <summary>
    /// Downloads the full JSON for a stored export. Returns null when not found.
    /// </summary>
    Task<string?> DownloadExportAsync(Guid exportId);

    /// <summary>
    /// Deletes a stored export. Returns true when an export was deleted.
    /// </summary>
    Task<bool> DeleteExportAsync(Guid exportId);
}
