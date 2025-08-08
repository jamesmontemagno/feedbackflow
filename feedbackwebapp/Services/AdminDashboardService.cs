using System.Text.Json;
using SharedDump.Models.Admin;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

/// <summary>
/// Web application service for admin dashboard metrics.
/// Communicates with Azure Functions backend API to retrieve aggregated
/// system metrics for admin users only.
/// 
/// This service handles:
/// - Authentication and authorization headers
/// - HTTP communication with Functions API
/// - Error handling and user-friendly error messages
/// - JSON serialization/deserialization
/// 
/// API Endpoints:
/// - GET /api/GetAdminDashboardMetrics - Get comprehensive dashboard metrics (Admin only)
/// </summary>
public class AdminDashboardService : IAdminDashboardService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<AdminDashboardService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public AdminDashboardService(
        HttpClient httpClient, 
        IAuthenticationHeaderService headerService,
        ILogger<AdminDashboardService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _headerService = headerService;
        _logger = logger;
        _baseUrl = configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("FeedbackApi:BaseUrl not configured");
        _functionsKey = configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("FeedbackApi:FunctionsKey not configured");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<AdminDashboardMetrics> GetDashboardMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving admin dashboard metrics from Functions API");

            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{_baseUrl}/api/GetAdminDashboardMetrics?code={Uri.EscapeDataString(_functionsKey)}");
            
            await _headerService.AddAuthenticationHeadersAsync(request);
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var metrics = JsonSerializer.Deserialize<AdminDashboardMetrics>(json, _jsonOptions);
                
                if (metrics == null)
                {
                    throw new InvalidOperationException("Failed to deserialize admin dashboard metrics");
                }

                _logger.LogInformation("Successfully retrieved admin dashboard metrics. Total users: {TotalUsers}", 
                    metrics.UserStats.TotalUsers);
                
                return metrics;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to retrieve admin dashboard metrics. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException("Admin access required to view dashboard metrics");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Authentication required to view dashboard metrics");
                }
                
                throw new HttpRequestException($"Failed to retrieve dashboard metrics: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error retrieving admin dashboard metrics");
            throw;
        }
    }
}