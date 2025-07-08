using System.Net;
using System.Text.Json;
using SharedDump.Models.Reports;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

public record RequestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public bool IsBadRequest => StatusCode == HttpStatusCode.BadRequest;
    public bool IsServerError => StatusCode >= HttpStatusCode.InternalServerError;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;
    
    public static RequestResult CreateSuccess() => new() { Success = true };
    
    public static RequestResult CreateError(string errorMessage, HttpStatusCode? statusCode = null) => 
        new() { Success = false, ErrorMessage = errorMessage, StatusCode = statusCode };
}

public interface IReportRequestService
{
    Task<IEnumerable<ReportModel>> GetUserReportsAsync();
    Task<RequestResult> AddReportRequestAsync(object request);
    Task<RequestResult> RemoveReportRequestAsync(string id);
    Task<IEnumerable<UserReportRequestModel>> GetUserReportRequestsAsync();
}

public class ReportRequestService : IReportRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportRequestService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IAuthenticationHeaderService authHeaderService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _authHeaderService = authHeaderService;
    }

    public async Task<IEnumerable<ReportModel>> GetUserReportsAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Get user reports code not configured");

            // Create the request message to add authentication headers
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/GetUserReports?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var reportsResponse = JsonSerializer.Deserialize<ReportsFilterResponse>(responseContent, _jsonOptions);

            return reportsResponse?.Reports ?? Enumerable.Empty<ReportModel>();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty collection to gracefully handle errors
            Console.WriteLine($"Error getting user reports: {ex.Message}");
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

            // Create the request message to add authentication headers
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/AddUserReportRequest?code={Uri.EscapeDataString(code)}")
            {
                Content = content
            };

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            
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

            // Create the request message to add authentication headers
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/api/userreportrequest/{Uri.EscapeDataString(id)}?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            
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

    public async Task<IEnumerable<UserReportRequestModel>> GetUserReportRequestsAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Get user report requests code not configured");

            // Create the request message to add authentication headers
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/ListUserReportRequests?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var requestsResponse = JsonSerializer.Deserialize<UserReportRequestsResponse>(responseContent, _jsonOptions);

            return requestsResponse?.Requests ?? Enumerable.Empty<UserReportRequestModel>();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty collection to gracefully handle errors
            Console.WriteLine($"Error getting user report requests: {ex.Message}");
            return Enumerable.Empty<UserReportRequestModel>();
        }
    }

    private class ReportsFilterResponse
    {
        public ReportModel[] Reports { get; set; } = Array.Empty<ReportModel>();
    }

    private class UserReportRequestsResponse
    {
        public UserReportRequestModel[] Requests { get; set; } = Array.Empty<UserReportRequestModel>();
    }
}