using System.Text.Json;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of <see cref="IReportDataAdminService"/> for local development and tests.
/// </summary>
public class MockReportDataAdminService : IReportDataAdminService
{
    private readonly ILogger<MockReportDataAdminService> _logger;

    private readonly List<ReportDataListItem> _reports = new()
    {
        new ReportDataListItem
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Source = "reddit",
            SubSource = "dotnet",
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CutoffDate = DateTimeOffset.UtcNow.AddDays(-8),
            ThreadCount = 5,
            CommentCount = 120,
            HasRawData = true
        },
        new ReportDataListItem
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Source = "reddit",
            SubSource = "csharp",
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-3),
            CutoffDate = DateTimeOffset.UtcNow.AddDays(-10),
            ThreadCount = 3,
            CommentCount = 64,
            HasRawData = false
        }
    };

    public MockReportDataAdminService(ILogger<MockReportDataAdminService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ReportDataListItem>> GetAllReportsAsync()
    {
        await Task.Delay(200);
        _logger.LogInformation("Mock: Returning {Count} sample reports", _reports.Count);
        return _reports.ToList();
    }

    public async Task<string?> DownloadRawDataAsync(Guid reportId)
    {
        await Task.Delay(200);
        var report = _reports.FirstOrDefault(r => r.Id == reportId);
        if (report is null || !report.HasRawData)
        {
            _logger.LogInformation("Mock: No raw data for report {ReportId}", reportId);
            return null;
        }

        var rawData = new RedditReportRawData
        {
            ReportId = report.Id,
            Subreddit = report.SubSource,
            GeneratedAt = report.GeneratedAt,
            CutoffDate = report.CutoffDate,
            ThreadCount = report.ThreadCount,
            CommentCount = report.CommentCount
        };

        return JsonSerializer.Serialize(rawData, new JsonSerializerOptions { WriteIndented = true });
    }
}
