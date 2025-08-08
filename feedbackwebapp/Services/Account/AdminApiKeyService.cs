using System.Text.Json;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// HTTP-based admin API key service that calls backend Functions APIs
/// </summary>
public class AdminApiKeyService : IAdminApiKeyService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly ILogger<AdminApiKeyService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public AdminApiKeyService(
        IHttpClientFactory httpClientFactory,
        IAuthenticationHeaderService authHeaderService,
        ILogger<AdminApiKeyService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _authHeaderService = authHeaderService;
        _logger = logger;
        
        _baseUrl = configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("Base URL not configured");
        _functionsKey = configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("Functions key not configured");
    }

    public async Task<List<AdminApiKeyInfo>?> GetAllApiKeysAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/GetAllApiKeysAdmin?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get all API keys: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<AdminApiKeyInfo>>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all API keys");
            return null;
        }
    }

    public async Task<bool> UpdateApiKeyStatusAsync(string apiKey, bool isEnabled)
    {
        try
        {
            var url = $"{_baseUrl}/api/UpdateApiKeyStatusAdmin?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var updateRequest = new { ApiKey = apiKey, IsEnabled = isEnabled };
            var json = JsonSerializer.Serialize(updateRequest);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update API key status: {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key status");
            return false;
        }
    }

    private class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}