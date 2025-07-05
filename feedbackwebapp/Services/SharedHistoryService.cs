using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Authentication;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;
using Microsoft.JSInterop;
using SharedDump.Models;

namespace FeedbackWebApp.Services;

/// <summary>
/// Provider for SharedHistoryService implementations
/// </summary>
public interface ISharedHistoryServiceProvider
{
    ISharedHistoryService GetService();
}

/// <summary>
/// Provider that returns either mock or real SharedHistoryService based on configuration
/// </summary>
public class SharedHistoryServiceProvider : ISharedHistoryServiceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SharedHistoryService> _logger;
    private readonly ILogger<MockSharedHistoryService> _mockLogger;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly bool _useMockService;

    public SharedHistoryServiceProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SharedHistoryService> logger,
        ILogger<MockSharedHistoryService> mockLogger,
        IAuthenticationHeaderService authHeaderService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _mockLogger = mockLogger;
        _authHeaderService = authHeaderService;
        _useMockService = configuration.GetValue<bool>("FeedbackApi:UseMocks", false);
    }

    public ISharedHistoryService GetService()
    {
        if (_useMockService)
            return new MockSharedHistoryService(_mockLogger);

        return new SharedHistoryService(_httpClientFactory, _logger, _configuration, _authHeaderService);
    }
}

/// <summary>
/// Service for managing user's saved shared analysis history
/// </summary>
public class SharedHistoryService : ISharedHistoryService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SharedHistoryService> _logger;
    private readonly IAuthenticationHeaderService _authHeaderService;
    protected readonly string BaseUrl;
    protected readonly IConfiguration Configuration;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SharedHistoryService(
        IHttpClientFactory httpClientFactory,
        ILogger<SharedHistoryService> logger,
        IConfiguration configuration,
        IAuthenticationHeaderService authHeaderService)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _logger = logger;
        _authHeaderService = authHeaderService;

        // Get base URLs from configuration
        Configuration = configuration;
        BaseUrl = Configuration?["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    /// <summary>
    /// Create an HTTP request message with authentication headers
    /// </summary>
    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        await _authHeaderService.AddAuthenticationHeadersAsync(request);
        return request;
    }

    /// <summary>
    /// Create an HTTP request message with authentication headers and content
    /// </summary>
    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string url, HttpContent content)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };
        await _authHeaderService.AddAuthenticationHeadersAsync(request);
        return request;
    }

    public async Task<List<SharedAnalysisEntity>> GetUsersSavedAnalysesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching user's saved analyses from server");

            var functionsKey = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("FunctionsKey not configured");

            var url = $"{BaseUrl}/api/GetUsersSavedAnalysis?code={Uri.EscapeDataString(functionsKey)}";
            
            var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch user's saved analyses: {StatusCode}", response.StatusCode);
                return new List<SharedAnalysisEntity>();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var analyses = JsonSerializer.Deserialize<List<SharedAnalysisEntity>>(responseContent, _jsonOptions);

            return analyses ?? new List<SharedAnalysisEntity>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user's saved analyses");
            return new List<SharedAnalysisEntity>();
        }
    }

    public async Task<bool> DeleteSharedAnalysisAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Cannot delete shared analysis: ID is null or empty");
            return false;
        }

        try
        {
            _logger.LogInformation("Deleting shared analysis {Id}", id);

            var functionsKey = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("FunctionsKey not configured");

            var url = $"{BaseUrl}/api/DeleteSharedAnalysis/{id}?code={Uri.EscapeDataString(functionsKey)}";
            
            var request = await CreateAuthenticatedRequestAsync(HttpMethod.Delete, url);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted shared analysis {Id}", id);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to delete shared analysis {Id}: {StatusCode}", id, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shared analysis {Id}", id);
            return false;
        }
    }

    public async Task<AnalysisData?> GetSharedAnalysisDataAsync(string id)
    {
        return await GetSharedAnalysisAsync(id);
    }

    public async Task<AnalysisData?> GetSharedAnalysisAsync(string id)
    {
        try
        {
            _logger.LogInformation("Getting shared analysis with ID: {Id}", id);

            var getSharedAnalysisCode = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("GetSharedAnalysisCode API code not configured");

            var getSharedPath = $"{BaseUrl}/api/GetSharedAnalysis/{id}?code={Uri.EscapeDataString(getSharedAnalysisCode)}";
            
            var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, getSharedPath);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get shared analysis: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var analysisData = JsonSerializer.Deserialize<AnalysisData>(responseContent, _jsonOptions);

            return analysisData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared analysis");
            return null;
        }
    }

    public async Task<int> GetSavedAnalysesCountAsync()
    {
        var analyses = await GetUsersSavedAnalysesAsync();
        return analyses.Count;
    }

    public async Task<List<SharedAnalysisEntity>> SearchUsersSavedAnalysesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetUsersSavedAnalysesAsync();
        }

        var allAnalyses = await GetUsersSavedAnalysesAsync();
        var searchTermLower = searchTerm.ToLowerInvariant();

        return allAnalyses.Where(analysis =>
            analysis.Title.ToLowerInvariant().Contains(searchTermLower) ||
            analysis.Summary.ToLowerInvariant().Contains(searchTermLower) ||
            analysis.SourceType.ToLowerInvariant().Contains(searchTermLower) ||
            (!string.IsNullOrEmpty(analysis.UserInput) && analysis.UserInput.ToLowerInvariant().Contains(searchTermLower))
        ).ToList();
    }

    public async Task<string> ShareAnalysisAsync(AnalysisData analysis, bool isPublic = false)
    {
        try
        {
            _logger.LogInformation("Sharing analysis (Public: {IsPublic})", isPublic);
            
            var requestData = new
            {
                Analysis = analysis,
                IsPublic = isPublic
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(requestData, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            
            var saveSharedAnalysisCode = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("SaveSharedAnalysisCode API code not configured");

            var saveSharedAnalysisUrl = $"{BaseUrl}/api/SaveSharedAnalysis?code={Uri.EscapeDataString(saveSharedAnalysisCode)}";

            var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, saveSharedAnalysisUrl, content);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ShareResponse>(responseContent, _jsonOptions);
            
            if (result == null || string.IsNullOrEmpty(result.Id))
            {
                throw new InvalidOperationException("Invalid response from sharing service");
            }

            return result.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing analysis");
            throw;
        }
    }

    public async Task<bool> UpdateAnalysisVisibilityAsync(string analysisId, bool isPublic)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            _logger.LogWarning("Cannot update analysis visibility: ID is null or empty");
            return false;
        }

        try
        {
            _logger.LogInformation("Updating analysis {Id} visibility to {IsPublic}", analysisId, isPublic);

            var requestData = new
            {
                IsPublic = isPublic
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(requestData, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var functionsKey = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("FunctionsKey not configured");

            var url = $"{BaseUrl}/api/UpdateAnalysisVisibility/{analysisId}?code={Uri.EscapeDataString(functionsKey)}";
            
            var request = await CreateAuthenticatedRequestAsync(HttpMethod.Patch, url, content);
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated analysis {Id} visibility to {IsPublic}", analysisId, isPublic);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to update analysis {Id} visibility: {StatusCode}", analysisId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating analysis {Id} visibility", analysisId);
            return false;
        }
    }

    public async Task<List<AnalysisHistoryItem>> GetSharedAnalysisHistoryAsync()
    {
        // Get shared analyses from cloud backend
        var sharedAnalyses = await GetUsersSavedAnalysesAsync();
        
        // Convert SharedAnalysisEntity to AnalysisHistoryItem format
        return sharedAnalyses.Select(entity => new AnalysisHistoryItem
        {
            Id = entity.Id,
            Timestamp = entity.CreatedDate,
            FullAnalysis = entity.Summary, // Use summary as the analysis content
            SourceType = entity.SourceType,
            UserInput = entity.UserInput ?? "",
            IsShared = true,
            SharedId = entity.Id,
            SharedDate = entity.CreatedDate
        }).ToList();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class ShareResponse
    {
        public string Id { get; set; } = string.Empty;
    }
}
