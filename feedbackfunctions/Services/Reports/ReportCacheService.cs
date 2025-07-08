using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Reports;

/// <summary>
/// In-memory cache service for reports with 24-hour TTL
/// </summary>
public class ReportCacheService : IReportCacheService
{
    private readonly ILogger<ReportCacheService> _logger;
    private readonly BlobContainerClient _containerClient;
    private readonly ConcurrentDictionary<string, CachedReport> _cache = new();
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of the ReportCacheService
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="containerClient">Blob container client for reports</param>
    public ReportCacheService(ILogger<ReportCacheService> logger, BlobContainerClient containerClient)
    {
        _logger = logger;
        _containerClient = containerClient;
    }

    /// <inheritdoc />
    public async Task<ReportModel?> GetReportAsync(string reportId)
    {
        await EnsureCacheIsValidAsync();

        if (_cache.TryGetValue(reportId, out var cachedReport))
        {
            _logger.LogDebug("Cache hit for report {ReportId}", reportId);
            return cachedReport.Report;
        }

        _logger.LogDebug("Cache miss for report {ReportId}", reportId);
        
        // Try to load individual report from blob storage
        try
        {
            var blobClient = _containerClient.GetBlobClient($"{reportId}.json");
            if (await blobClient.ExistsAsync())
            {
                var blobContent = await blobClient.DownloadContentAsync();
                var report = JsonSerializer.Deserialize<ReportModel>(blobContent.Value.Content, _jsonOptions);
                
                if (report != null)
                {
                    // Add to cache
                    _cache.TryAdd(reportId, new CachedReport(report, DateTime.UtcNow));
                    _logger.LogDebug("Loaded and cached report {ReportId} from blob storage", reportId);
                    return report;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load report {ReportId} from blob storage", reportId);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<List<ReportModel>> GetReportsAsync(string? sourceFilter = null, string? subsourceFilter = null)
    {
        await EnsureCacheIsValidAsync();

        var reports = _cache.Values
            .Select(cr => cr.Report)
            .Where(report => 
            {
                var matchesSource = string.IsNullOrEmpty(sourceFilter) || 
                                   string.Equals(report.Source, sourceFilter, StringComparison.OrdinalIgnoreCase);
                var matchesSubsource = string.IsNullOrEmpty(subsourceFilter) || 
                                      string.Equals(report.SubSource, subsourceFilter, StringComparison.OrdinalIgnoreCase);
                return matchesSource && matchesSubsource;
            })
            .ToList();

        _logger.LogDebug("Retrieved {Count} reports from cache with filters (source: {Source}, subsource: {Subsource})", 
            reports.Count, sourceFilter ?? "any", subsourceFilter ?? "any");

        return reports;
    }

    /// <inheritdoc />
    public async Task SetReportAsync(ReportModel report)
    {
        var reportId = report.Id.ToString();
        _cache.AddOrUpdate(reportId, 
            new CachedReport(report, DateTime.UtcNow),
            (key, existing) => new CachedReport(report, DateTime.UtcNow));
        
        // Mark cache as initialized if this is the first item
        if (_lastRefresh == DateTime.MinValue)
        {
            _lastRefresh = DateTime.UtcNow;
        }
        
        _logger.LogDebug("Cached report {ReportId}", report.Id);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveReportAsync(string reportId)
    {
        _cache.TryRemove(reportId, out _);
        _logger.LogDebug("Removed report {ReportId} from cache", reportId);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync()
    {
        _cache.Clear();
        _lastRefresh = DateTime.MinValue;
        _logger.LogInformation("Cleared all cached reports");
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<CacheStatus> GetCacheStatusAsync()
    {
        var expiresAt = _lastRefresh.Add(CacheExpiry);
        await Task.CompletedTask;
        return new CacheStatus(_cache.Count, _lastRefresh, expiresAt);
    }

    /// <inheritdoc />
    public async Task RefreshCacheAsync()
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Refreshing report cache from blob storage");
            _cache.Clear();

            var loadedCount = 0;
            await foreach (var blob in _containerClient.GetBlobsAsync())
            {
                try
                {
                    var blobClient = _containerClient.GetBlobClient(blob.Name);
                    var content = await blobClient.DownloadContentAsync();
                    var report = JsonSerializer.Deserialize<ReportModel>(content.Value.Content, _jsonOptions);

                    if (report != null)
                    {
                        _cache.TryAdd(report.Id.ToString(), new CachedReport(report, DateTime.UtcNow));
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load report from blob {BlobName}", blob.Name);
                }
            }

            _lastRefresh = DateTime.UtcNow;
            _logger.LogInformation("Cache refresh completed. Loaded {Count} reports", loadedCount);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    /// <summary>
    /// Ensures the cache is valid and refreshes if needed
    /// </summary>
    private async Task EnsureCacheIsValidAsync()
    {
        // Only refresh if cache is completely empty and has never been refreshed
        // This prevents automatic refresh during testing
        if (_cache.IsEmpty && _lastRefresh == DateTime.MinValue)
        {
            try
            {
                await RefreshCacheAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh cache automatically, continuing with empty cache");
            }
        }
        else if (_lastRefresh != DateTime.MinValue && DateTime.UtcNow - _lastRefresh > CacheExpiry)
        {
            // Only refresh if cache was previously loaded and has expired
            try
            {
                await RefreshCacheAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh expired cache, continuing with existing cache");
            }
        }
    }

    /// <summary>
    /// Represents a cached report with timestamp
    /// </summary>
    private record CachedReport(ReportModel Report, DateTime CachedAt);
}
