using System.Text.Json;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

public interface IReportRequestService
{
    Task<IEnumerable<ReportModel>> FilterReportsAsync(IEnumerable<object> userRequests);
    Task<bool> AddReportRequestAsync(object request);
    Task<bool> RemoveReportRequestAsync(string id);
}

public class ReportRequestService : IReportRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportRequestService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
    }

    public async Task<IEnumerable<ReportModel>> FilterReportsAsync(IEnumerable<object> userRequests)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FilterReportsCode"]
                ?? throw new InvalidOperationException("Filter reports code not configured");

            var json = JsonSerializer.Serialize(userRequests, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/api/FilterReports?code={Uri.EscapeDataString(code)}", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var reportsResponse = JsonSerializer.Deserialize<ReportsFilterResponse>(responseContent, _jsonOptions);

            return reportsResponse?.Reports ?? Enumerable.Empty<ReportModel>();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty collection to gracefully handle errors
            Console.WriteLine($"Error filtering reports: {ex.Message}");
            return Enumerable.Empty<ReportModel>();
        }
    }

    public async Task<bool> AddReportRequestAsync(object request)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:AddReportRequestCode"]
                ?? throw new InvalidOperationException("Add report request code not configured");

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/api/AddReportRequest?code={Uri.EscapeDataString(code)}", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding report request: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveReportRequestAsync(string id)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:RemoveReportRequestCode"]
                ?? throw new InvalidOperationException("Remove report request code not configured");

            var response = await _httpClient.DeleteAsync($"{baseUrl}/api/reportrequest/{Uri.EscapeDataString(id)}?code={Uri.EscapeDataString(code)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing report request {id}: {ex.Message}");
            return false;
        }
    }

    private class ReportsFilterResponse
    {
        public ReportModel[] Reports { get; set; } = Array.Empty<ReportModel>();
    }
}