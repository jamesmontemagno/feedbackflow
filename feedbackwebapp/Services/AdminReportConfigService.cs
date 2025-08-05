using System.Net.Mime;
using System.Text;
using System.Text.Json;
using SharedDump.Models.Reports;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

/// <summary>
/// Web application service for managing admin report configurations.
/// Communicates with Azure Functions backend API to perform CRUD operations
/// on automated report configurations that admin users can create.
/// 
/// This service handles:
/// - Authentication and authorization headers
/// - HTTP communication with Functions API
/// - Error handling and user-friendly error messages
/// - JSON serialization/deserialization
/// 
/// API Endpoints:
/// - GET    /api/admin/get-reports        - Get all admin report configurations
/// - POST   /api/admin/create-report      - Create a new admin report configuration
/// - PUT    /api/admin/update-report/{id} - Update an existing admin report configuration
/// - DELETE /api/admin/delete-report/{id} - Delete an admin report configuration
/// </summary>

public class AdminReportConfigService : IAdminReportConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<AdminReportConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public AdminReportConfigService(
        HttpClient httpClient, 
        IAuthenticationHeaderService headerService,
        ILogger<AdminReportConfigService> logger,
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
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<AdminReportConfigModel>> GetAllConfigsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{_baseUrl}/api/admin/get-reports?code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<AdminReportConfigModel>>(content, _jsonOptions) ?? new List<AdminReportConfigModel>();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var statusCodeMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Authentication required",
                    System.Net.HttpStatusCode.Forbidden => "Admin access required",
                    _ => $"Server error ({response.StatusCode})"
                };
                
                _logger.LogError("Failed to get admin report configs. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to get admin report configs: {statusCodeMessage} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin report configs");
            throw;
        }
    }

    public async Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{_baseUrl}/api/admin/create-report?code={Uri.EscapeDataString(_functionsKey)}")
            {
                Content = content
            };
            await _headerService.AddAuthenticationHeadersAsync(request);
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AdminReportConfigModel>(responseContent, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize created config");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var statusCodeMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Invalid configuration data",
                    System.Net.HttpStatusCode.Unauthorized => "Authentication required",
                    System.Net.HttpStatusCode.Forbidden => "Admin access required",
                    _ => $"Server error ({response.StatusCode})"
                };
                
                _logger.LogError("Failed to create admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to create admin report config: {statusCodeMessage} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin report config");
            throw;
        }
    }

    public async Task<AdminReportConfigModel> UpdateConfigAsync(AdminReportConfigModel config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            
            var request = new HttpRequestMessage(HttpMethod.Put, 
                $"{_baseUrl}/api/admin/update-report/{Uri.EscapeDataString(config.Id)}?code={Uri.EscapeDataString(_functionsKey)}")
            {
                Content = content
            };
            await _headerService.AddAuthenticationHeadersAsync(request);
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AdminReportConfigModel>(responseContent, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize updated config");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var statusCodeMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Invalid request data",
                    System.Net.HttpStatusCode.Unauthorized => "Authentication required",
                    System.Net.HttpStatusCode.Forbidden => "Admin access required",
                    System.Net.HttpStatusCode.NotFound => "Configuration not found",
                    _ => $"Server error ({response.StatusCode})"
                };
                
                _logger.LogError("Failed to update admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update admin report config: {statusCodeMessage} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin report config: {Id}", config.Id);
            throw;
        }
    }

    public async Task<bool> DeleteConfigAsync(string id)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, 
                $"{_baseUrl}/api/admin/delete-report/{Uri.EscapeDataString(id)}?code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var statusCodeMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Invalid request",
                    System.Net.HttpStatusCode.Unauthorized => "Authentication required",
                    System.Net.HttpStatusCode.Forbidden => "Admin access required",
                    System.Net.HttpStatusCode.NotFound => "Configuration not found",
                    _ => $"Server error ({response.StatusCode})"
                };
                
                _logger.LogError("Failed to delete admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to delete admin report config: {statusCodeMessage} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting admin report config: {Id}", id);
            throw;
        }
    }
}
