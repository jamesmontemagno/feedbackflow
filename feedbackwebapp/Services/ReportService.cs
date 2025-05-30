using System.Text.Json;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

public interface IReportService
{
    Task<IEnumerable<ReportModel>> ListReportsAsync();
    Task<ReportModel?> GetReportAsync(string id);
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
        var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
        var code = _configuration["FeedbackApi:ListReportsCode"]
            ?? throw new InvalidOperationException("List reports code not configured");

        var url = $"{baseUrl}/api/ListReports?code={Uri.EscapeDataString(code)}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ReportsResponse>(content, _jsonOptions);
        return result?.Reports ?? Enumerable.Empty<ReportModel>();
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
