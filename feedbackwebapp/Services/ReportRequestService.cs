using System.Net;
using System.Text.Json;
using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

public record RequestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public bool IsBadRequest => StatusCode == HttpStatusCode.BadRequest;
    public bool IsServerError => StatusCode >= HttpStatusCode.InternalServerError;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    
    public static RequestResult CreateSuccess() => new() { Success = true };
    
    public static RequestResult CreateError(string errorMessage, HttpStatusCode? statusCode = null) => 
        new() { Success = false, ErrorMessage = errorMessage, StatusCode = statusCode };
}

public interface IReportRequestService
{
    Task<IEnumerable<ReportModel>> FilterReportsAsync(IEnumerable<object> userRequests);
    Task<RequestResult> AddReportRequestAsync(object request);
    Task<RequestResult> RemoveReportRequestAsync(string id);
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
            var code = _configuration["FeedbackApi:FunctionsKey"]
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

    public async Task<RequestResult> AddReportRequestAsync(object request)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Add report request code not configured");

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/api/AddReportRequest?code={Uri.EscapeDataString(code)}", content);
            
            if (response.IsSuccessStatusCode)
            {
                return RequestResult.CreateSuccess();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = string.IsNullOrWhiteSpace(errorContent) 
                ? $"Request failed with status {response.StatusCode}" 
                : errorContent;
            
            return RequestResult.CreateError(errorMessage, response.StatusCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding report request: {ex.Message}");
            return RequestResult.CreateError($"Network error: {ex.Message}");
        }
    }

    public async Task<RequestResult> RemoveReportRequestAsync(string id)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Remove report request code not configured");

            var response = await _httpClient.DeleteAsync($"{baseUrl}/api/reportrequest/{Uri.EscapeDataString(id)}?code={Uri.EscapeDataString(code)}");
            
            if (response.IsSuccessStatusCode)
            {
                return RequestResult.CreateSuccess();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = string.IsNullOrWhiteSpace(errorContent) 
                ? $"Request failed with status {response.StatusCode}" 
                : errorContent;
            
            return RequestResult.CreateError(errorMessage, response.StatusCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing report request {id}: {ex.Message}");
            return RequestResult.CreateError($"Network error: {ex.Message}");
        }
    }

    private class ReportsFilterResponse
    {
        public ReportModel[] Reports { get; set; } = Array.Empty<ReportModel>();
    }
}