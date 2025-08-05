# Report Caching Implementation

## Overview
The report caching system has been implemented to improve performance by reducing the need to repeatedly read reports from Azure Blob Storage. Since reports are typically generated once a week or on demand, caching them in memory provides significant performance benefits.

## Features

### In-Memory Cache
- Uses `ConcurrentDictionary` for thread-safe operations
- 24-hour Time-To-Live (TTL) for cached reports
- Automatic cache expiration and refresh
- Lazy loading of individual reports

### Cache Service Interface (`IReportCacheService`)
- `GetReportAsync(string reportId)` - Get a single report by ID
- `GetReportsAsync(string? sourceFilter, string? subsourceFilter)` - Get filtered reports
- `SetReportAsync(ReportModel report)` - Add/update a report in cache
- `RemoveReportAsync(string reportId)` - Remove a report from cache
- `ClearCacheAsync()` - Clear all cached reports
- `RefreshCacheAsync()` - Manually refresh cache from blob storage
- `GetCacheStatusAsync()` - Get cache statistics

### Cache Behavior
1. **Cache Miss**: If a report is not in cache, it's loaded from blob storage and cached
2. **Cache Expiry**: After 24 hours, the cache is automatically refreshed
3. **New Reports**: When reports are generated, they're automatically added to the cache
4. **Thread Safety**: All operations are thread-safe using `ConcurrentDictionary`

## API Endpoints

### Cache Management Endpoints (`ReportCacheFunctions`)
- `GET /api/cache/status` - Get cache status and statistics
- `POST /api/cache/refresh` - Manually refresh the cache
- `POST /api/cache/clear` - Clear the cache

### Report Endpoints (`ReportingFunctions`)
All existing report endpoints now use the cache:
- `GET /api/Report/{id}` - Uses cache for individual report retrieval
- `GET /api/GetUserReports` - Uses cache for user-specific report retrieval

## Implementation Details

### Service Registration
The cache service is registered as a singleton in `Program.cs` to ensure cache persistence across function invocations within the same process.

### Report Generator Integration
The `ReportGenerator` has been updated to accept an optional `IReportCacheService` parameter. When a report is stored, it's automatically added to the cache.

### Error Handling
- Cache failures don't prevent normal operation
- Falls back to blob storage if cache operations fail
- Comprehensive logging for cache operations

## Performance Benefits
- Reduces blob storage read operations for frequently accessed reports
- Improves response times for report listing and retrieval
- Particularly beneficial for the weekly report generation pattern
- Supports high-frequency access patterns

## Configuration
The cache TTL is set to 24 hours and can be modified in the `ReportCacheService` class by changing the `CacheExpiry` constant.

## Monitoring
Use the cache status endpoint to monitor:
- Number of cached reports
- Last refresh time
- Cache expiration time
- Whether the cache is expired
