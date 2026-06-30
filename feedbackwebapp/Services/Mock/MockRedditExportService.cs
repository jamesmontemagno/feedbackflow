using System.Text.Json;
using SharedDump.Models.Reddit;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of <see cref="IRedditExportService"/> for local development and tests.
/// </summary>
public class MockRedditExportService : IRedditExportService
{
    private readonly ILogger<MockRedditExportService> _logger;

    private readonly List<RedditExportListItem> _exports = new()
    {
        new RedditExportListItem
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Subreddit = "dotnet",
            StartDate = DateTimeOffset.UtcNow.AddDays(-7),
            EndDate = DateTimeOffset.UtcNow,
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ThreadCount = 42,
            CommentCount = 1380,
            ThreadLimitReached = false
        },
        new RedditExportListItem
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Subreddit = "csharp",
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow.AddDays(-2),
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ThreadCount = 200,
            CommentCount = 9123,
            ThreadLimitReached = true
        }
    };

    public MockRedditExportService(ILogger<MockRedditExportService> logger)
    {
        _logger = logger;
    }

    public async Task<RedditExportListItem> CreateExportAsync(CreateRedditExportRequest request)
    {
        await Task.Delay(800);
        var item = new RedditExportListItem
        {
            Id = Guid.NewGuid(),
            Subreddit = request.Subreddit,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            GeneratedAt = DateTimeOffset.UtcNow,
            ThreadCount = 12,
            CommentCount = 256,
            ThreadLimitReached = false
        };
        _exports.Insert(0, item);
        _logger.LogInformation("Mock: Created export for r/{Subreddit}", request.Subreddit);
        return item;
    }

    public async Task<List<RedditExportListItem>> GetExportsAsync()
    {
        await Task.Delay(200);
        _logger.LogInformation("Mock: Returning {Count} sample exports", _exports.Count);
        return _exports.OrderByDescending(e => e.GeneratedAt).ToList();
    }

    public async Task<string?> DownloadExportAsync(Guid exportId)
    {
        await Task.Delay(200);
        var item = _exports.FirstOrDefault(e => e.Id == exportId);
        if (item is null)
        {
            _logger.LogInformation("Mock: No export found for {ExportId}", exportId);
            return null;
        }

        var export = new RedditSubredditExport
        {
            Id = item.Id,
            Subreddit = item.Subreddit,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            GeneratedAt = item.GeneratedAt,
            ThreadCount = item.ThreadCount,
            CommentCount = item.CommentCount,
            ThreadLimitReached = item.ThreadLimitReached,
            SubredditInfo = new RedditSubredditInfo
            {
                DisplayName = item.Subreddit,
                Title = $"r/{item.Subreddit}",
                PublicDescription = $"Mock export for r/{item.Subreddit}"
            }
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<bool> DeleteExportAsync(Guid exportId)
    {
        await Task.Delay(150);
        var item = _exports.FirstOrDefault(e => e.Id == exportId);
        if (item is null)
        {
            return false;
        }
        _exports.Remove(item);
        _logger.LogInformation("Mock: Deleted export {ExportId}", exportId);
        return true;
    }
}
