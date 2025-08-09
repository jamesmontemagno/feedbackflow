using System.Text.Json;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.Account;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// HTTP-based admin user tier service that calls backend Functions APIs.
/// </summary>
public class AdminUserTierService : IAdminUserTierService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly ILogger<AdminUserTierService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public AdminUserTierService(
        IHttpClientFactory httpClientFactory,
        IAuthenticationHeaderService authHeaderService,
        ILogger<AdminUserTierService> logger,
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

    public async Task<List<AdminUserTierInfo>?> GetAllUserTiersAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/GetAllUserTiersAdmin?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user tiers: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<AdminUserTierInfo>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user tiers");
            return null;
        }
    }

    public async Task<bool> UpdateUserTierAsync(string userId, AccountTier newTier)
    {
        try
        {
            var url = $"{_baseUrl}/api/UpdateUserTierAdmin?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var payload = new { UserId = userId, Tier = newTier };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update user tier {UserId}: {StatusCode}", userId, response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return apiResponse?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user tier {UserId}", userId);
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
