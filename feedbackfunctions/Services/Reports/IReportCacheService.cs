using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Reports;

/// <summary>
/// Interface for caching report data in memory
/// </summary>
public interface IReportCacheService
{
    /// <summary>
    /// Gets a report from cache by ID
    /// </summary>
    /// <param name="reportId">The report ID</param>
    /// <returns>The cached report or null if not found</returns>
    Task<ReportModel?> GetReportAsync(string reportId);

    /// <summary>
    /// Gets all cached reports with optional filtering
    /// </summary>
    /// <param name="sourceFilter">Optional source filter</param>
    /// <param name="subsourceFilter">Optional subsource filter</param>
    /// <returns>List of cached reports matching the filters</returns>
    Task<List<ReportModel>> GetReportsAsync(string? sourceFilter = null, string? subsourceFilter = null);

    /// <summary>
    /// Adds or updates a report in the cache
    /// </summary>
    /// <param name="report">The report to cache</param>
    Task SetReportAsync(ReportModel report);

    /// <summary>
    /// Removes a report from the cache
    /// </summary>
    /// <param name="reportId">The report ID to remove</param>
    Task RemoveReportAsync(string reportId);

    /// <summary>
    /// Clears all cached reports
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Gets the cache status including count and last refresh time
    /// </summary>
    Task<CacheStatus> GetCacheStatusAsync();

    /// <summary>
    /// Forces a refresh of the cache from blob storage
    /// </summary>
    Task RefreshCacheAsync();
}

/// <summary>
/// Cache status information
/// </summary>
public record CacheStatus(int ReportCount, DateTime LastRefresh, DateTime ExpiresAt);
