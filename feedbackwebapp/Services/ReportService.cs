using System.Text.Json;
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
    private readonly bool _useMockService;

    public ReportServiceProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _useMockService = configuration.GetValue<bool>("FeedbackApi:UseMocks", false);
    }

    public IReportService GetService()
    {
        if (_useMockService)
            return new MockReportService();

        return new ReportService(_httpClientFactory, _configuration);
    }
}

public interface IReportService
{
    Task<IEnumerable<ReportModel>> ListReportsAsync();
    Task<IEnumerable<ReportModel>> ListReportsAsync(string? source = null, string? subsource = null);
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

    public Task<IEnumerable<ReportModel>> ListReportsAsync()
    {
        return ListReportsAsync(null, null);
    }

    public Task<IEnumerable<ReportModel>> ListReportsAsync(string? source = null, string? subsource = null)
    {
        var reports = _mockReports.AsEnumerable();
        
        if (!string.IsNullOrEmpty(source))
            reports = reports.Where(r => r.Source == source);
            
        if (!string.IsNullOrEmpty(subsource))
            reports = reports.Where(r => r.SubSource == subsource);

        return Task.FromResult(reports);
    }

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
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _configuration = configuration;
    }

    public async Task<IEnumerable<ReportModel>> ListReportsAsync()
    {
        return await ListReportsAsync(null, null);
    }

    public async Task<IEnumerable<ReportModel>> ListReportsAsync(string? source = null, string? subsource = null)
    {
        var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
        var code = _configuration["FeedbackApi:ListReportsCode"]
            ?? throw new InvalidOperationException("List reports code not configured");

        var queryParams = new List<string> { $"code={Uri.EscapeDataString(code)}" };
        if (!string.IsNullOrEmpty(source))
            queryParams.Add($"source={Uri.EscapeDataString(source)}");
        if (!string.IsNullOrEmpty(subsource))
            queryParams.Add($"subsource={Uri.EscapeDataString(subsource)}");

        var url = $"{baseUrl}/api/ListReports?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ReportsResponse>(content, _jsonOptions);
        return (result?.Reports ?? Enumerable.Empty<ReportModel>())
            .OrderByDescending(r => r.GeneratedAt);
    }

    public async Task<ReportModel?> GetReportAsync(string id)
    {
        var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
        var code = _configuration["FeedbackApi:GetReportCode"]
            ?? throw new InvalidOperationException("Get report code not configured");

        var url = $"{baseUrl}/api/Report/{id}?code={Uri.EscapeDataString(code)}";
        var response = await _httpClient.GetAsync(url);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
            
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ReportModel>(content, _jsonOptions);
    }

    private class ReportsResponse
    {
        public ReportModel[] Reports { get; set; } = Array.Empty<ReportModel>();
    }
}
