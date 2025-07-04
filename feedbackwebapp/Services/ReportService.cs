using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

public interface IReportServiceProvider
{
    IReportService GetService();
}

public class ReportServiceProvider : IReportServiceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly bool _useMockService;

    public ReportServiceProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMemoryCache memoryCache)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _useMockService = configuration.GetValue<bool>("FeedbackApi:UseMocks", false);
    }

    public IReportService GetService()
    {
        if (_useMockService)
            return new MockReportService();

        return new ReportService(_httpClientFactory, _configuration, _memoryCache);
    }
}

public interface IReportService
{
    Task<ReportModel?> GetReportAsync(string id);
}

public class MockReportService : IReportService
{
    private readonly List<ReportModel> _mockReports = new()
    {
        new ReportModel
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Source = "reddit",
            SubSource = "dotnet",
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
            HtmlContent = "<h1>Mock Report 1</h1><p>This is a mock report for testing</p>",
            ThreadCount = 5,
            CommentCount = 25,
            CutoffDate = DateTimeOffset.UtcNow.AddDays(-7)
        },
        new ReportModel
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Source = "reddit",
            SubSource = "githubcopilot",
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-2),
            HtmlContent = "<h1>Mock Report 2</h1><p>Another mock report for testing</p>",
            ThreadCount = 3,
            CommentCount = 15,
            CutoffDate = DateTimeOffset.UtcNow.AddDays(-7)
        }
    };

    public Task<ReportModel?> GetReportAsync(string id)
    {
        if (!Guid.TryParse(id, out var guidId))
            return Task.FromResult<ReportModel?>(null);

        var report = _mockReports.FirstOrDefault(r => r.Id == guidId);
        return Task.FromResult(report);
    }
}

public class ReportService : IReportService
{
    private const string ReportCacheKeyPrefix = "report_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(4); // Cache for 4 hours since reports don't change

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _configuration = configuration;
        _memoryCache = memoryCache;
    }

    public async Task<ReportModel?> GetReportAsync(string id)
    {
        // Create cache key
        var cacheKey = $"{ReportCacheKeyPrefix}{id}";

        // Check cache first
        if (_memoryCache.TryGetValue(cacheKey, out ReportModel? cachedReport))
        {
            return cachedReport;
        }

        var baseUrl = _configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("API base URL not configured");
        var code = _configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("Get report code not configured");

        var url = $"{baseUrl}/api/Report/{id}?code={Uri.EscapeDataString(code)}";
        var response = await _httpClient.GetAsync(url);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var report = JsonSerializer.Deserialize<ReportModel>(content, _jsonOptions);

        // Cache the result if it's not null
        if (report != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration)
                .SetPriority(CacheItemPriority.Normal);
            _memoryCache.Set(cacheKey, report, cacheOptions);
        }

        return report;
    }
}
