using System.Text.Json;
using SharedDump.Models.Account;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Account;

/// <summary>
/// HTTP-based account service that calls backend Functions APIs
/// </summary>
public class WebAppAccountService : IWebAppAccountService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly ILogger<WebAppAccountService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public WebAppAccountService(
        IHttpClientFactory httpClientFactory,
        IAuthenticationHeaderService authHeaderService,
        ILogger<WebAppAccountService> logger,
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

    public async Task<(UserAccount? account, AccountLimits limits)?> GetUserAccountAndLimitsAsync(TierInfo[]? tierInfo = null)
    {
        try
        {
            // Get user account
            var accountUrl = $"{_baseUrl}/api/GetUserAccount?code={Uri.EscapeDataString(_functionsKey)}";
            var accountRequest = new HttpRequestMessage(HttpMethod.Get, accountUrl);
            await _authHeaderService.AddAuthenticationHeadersAsync(accountRequest);

            var accountResponse = await _httpClient.SendAsync(accountRequest);
            if (!accountResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user account: {StatusCode}", accountResponse.StatusCode);
                return null;
            }

            var accountJsonContent = await accountResponse.Content.ReadAsStringAsync();
            var accountResult = JsonSerializer.Deserialize<ApiResponse<UserAccount>>(accountJsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (accountResult?.Data == null)
            {
                _logger.LogWarning("User account data is null");
                return null;
            }

            AccountLimits limits;

            // If tier info is provided, use it to find limits for the user's tier
            if (tierInfo != null)
            {
                var userTierInfo = tierInfo.FirstOrDefault(t => t.Tier == accountResult.Data.Tier);
                if (userTierInfo != null)
                {
                    limits = new AccountLimits
                    {
                        AnalysisLimit = userTierInfo.Limits.AnalysisLimit,
                        ReportLimit = userTierInfo.Limits.ReportLimit,
                        FeedQueryLimit = userTierInfo.Limits.FeedQueryLimit,
                        AnalysisRetentionDays = userTierInfo.Limits.AnalysisRetentionDays
                    };
                }
                else
                {
                    _logger.LogWarning("User tier {Tier} not found in provided tier info, using defaults", accountResult.Data.Tier);
                    limits = GetDefaultLimits();
                }
            }
            else
            {
                // Fallback to API call if tier info not provided
                var limitsUrl = $"{_baseUrl}/api/GetTierLimits?code={Uri.EscapeDataString(_functionsKey)}&tier={(int)accountResult.Data.Tier}";
                var limitsRequest = new HttpRequestMessage(HttpMethod.Get, limitsUrl);
                await _authHeaderService.AddAuthenticationHeadersAsync(limitsRequest);

                var limitsResponse = await _httpClient.SendAsync(limitsRequest);
                
                if (limitsResponse.IsSuccessStatusCode)
                {
                    var limitsJson = await limitsResponse.Content.ReadAsStringAsync();
                    var limitsResult = JsonSerializer.Deserialize<ApiResponse<AccountLimits>>(limitsJson, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    limits = limitsResult?.Data ?? GetDefaultLimits();
                }
                else
                {
                    _logger.LogWarning("Failed to get tier limits: {StatusCode}, using defaults", limitsResponse.StatusCode);
                    limits = GetDefaultLimits();
                }
            }

            return (accountResult.Data, limits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user account and limits");
            return null;
        }
    }

    public async Task<TierInfo[]?> GetTierLimitsAsync()
    {
        try
        {
            var tierLimitsUrl = $"{_baseUrl}/api/GetTierLimits?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Get, tierLimitsUrl);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get tier limits: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<TierInfo[]>>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tier limits");
            return null;
        }
    }

    public async Task<ApiKey?> GetApiKeyAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/GetUserApiKey?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    // User doesn't have an API key or doesn't have permission
                    return null;
                }
                
                _logger.LogWarning("Failed to get API key: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<ApiKey>>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key");
            return null;
        }
    }

    public async Task<ApiKey?> CreateApiKeyAsync(string? name = null)
    {
        try
        {
            var url = $"{_baseUrl}/api/CreateUserApiKey?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            if (!string.IsNullOrEmpty(name))
            {
                var requestBody = JsonSerializer.Serialize(new { Name = name });
                request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to create API key: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<ApiKey>>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            return null;
        }
    }

    public async Task<bool> DeleteApiKeyAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/DeleteUserApiKey?code={Uri.EscapeDataString(_functionsKey)}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            await _authHeaderService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to delete API key: {StatusCode}", response.StatusCode);
                return false;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return apiResponse?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API key");
            return false;
        }
    }

    private static AccountLimits GetDefaultLimits()
    {
        return new AccountLimits
        {
            AnalysisLimit = 10,
            ReportLimit = 1,
            FeedQueryLimit = 20,
            AnalysisRetentionDays = 30
        };
    }

    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    private class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
