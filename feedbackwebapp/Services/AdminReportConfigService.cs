using System.Text;
using System.Text.Json;
using SharedDump.Models.Reports;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

public class AdminReportConfigService : IAdminReportConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<AdminReportConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdminReportConfigService(
        HttpClient httpClient, 
        IAuthenticationHeaderService headerService,
        ILogger<AdminReportConfigService> logger)
    {
        _httpClient = httpClient;
        _headerService = headerService;
        _logger = logger;
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
            var request = new HttpRequestMessage(HttpMethod.Get, "api/GetAdminReportConfigs");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var configs = JsonSerializer.Deserialize<List<AdminReportConfigModel>>(content, _jsonOptions);
                return configs ?? new List<AdminReportConfigModel>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to admin report configs - user may not be admin");
                return new List<AdminReportConfigModel>();
            }
            else
            {
                _logger.LogError("Failed to get admin report configs. Status: {StatusCode}", response.StatusCode);
                return new List<AdminReportConfigModel>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin report configs");
            return new List<AdminReportConfigModel>();
        }
    }

    public async Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "api/CreateAdminReportConfig")
            {
                Content = content
            };
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var createdConfig = JsonSerializer.Deserialize<AdminReportConfigModel>(responseContent, _jsonOptions);
                return createdConfig ?? config;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to create admin report config: {errorContent}");
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
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"api/admin-report-configs/{config.Id}")
            {
                Content = content
            };
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var updatedConfig = JsonSerializer.Deserialize<AdminReportConfigModel>(responseContent, _jsonOptions);
                return updatedConfig ?? config;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update admin report config: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating admin report config");
            throw;
        }
    }

    public async Task<bool> DeleteConfigAsync(string id)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"api/admin-report-configs/{id}");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete admin report config. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting admin report config");
            return false;
        }
    }
}