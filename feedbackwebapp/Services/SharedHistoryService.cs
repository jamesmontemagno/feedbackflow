using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SharedHistoryService> _logger;
    private readonly ILogger<MockSharedHistoryService> _mockLogger;
    private readonly bool _useMockService;

    public SharedHistoryServiceProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<SharedHistoryService> logger,
        ILogger<MockSharedHistoryService> mockLogger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
        _mockLogger = mockLogger;
        _useMockService = configuration.GetValue<bool>("FeedbackApi:UseMocks", false);
    }

    public ISharedHistoryService GetService()
    {
        if (_useMockService)
            return new MockSharedHistoryService(_mockLogger);

        return new SharedHistoryService(_httpClientFactory, _logger, _configuration, _memoryCache);
    }
}

/// <summary>
/// Service for managing user's saved shared analysis history
/// </summary>
public class SharedHistoryService : ISharedHistoryService, IDisposable
{
    private const string CacheKey = "user_saved_analyses";
    private const string CacheKeyPrefix = "shared_analysis_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10); // Cache for 10 minutes
    private static readonly TimeSpan SharedAnalysisCacheDuration = TimeSpan.FromHours(4); // Cache for 4 hours since analyses don't change

    private readonly HttpClient _httpClient;
    private readonly ILogger<SharedHistoryService> _logger;
    private readonly IMemoryCache _memoryCache;
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
        IMemoryCache memoryCache)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _logger = logger;
        _memoryCache = memoryCache;

        // Get base URLs from configuration
        Configuration = configuration;
        BaseUrl = Configuration?["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    public async Task<List<SharedAnalysisEntity>> GetUsersSavedAnalysesAsync()
    {
        // Check cache first
        if (_memoryCache.TryGetValue(CacheKey, out List<SharedAnalysisEntity>? cachedAnalyses))
        {
            _logger.LogInformation("Retrieved user's saved analyses from cache");
            return cachedAnalyses ?? new List<SharedAnalysisEntity>();
        }

        try
        {
            _logger.LogInformation("Fetching user's saved analyses from server");

            var functionsKey = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("FunctionsKey not configured");

            var url = $"{BaseUrl}/api/GetUsersSavedAnalysis?code={Uri.EscapeDataString(functionsKey)}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch user's saved analyses: {StatusCode}", response.StatusCode);
                return new List<SharedAnalysisEntity>();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var analyses = JsonSerializer.Deserialize<List<SharedAnalysisEntity>>(responseContent, _jsonOptions);

            if (analyses != null)
            {
                // Cache the result
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(CacheDuration)
                    .SetPriority(CacheItemPriority.Normal);
                _memoryCache.Set(CacheKey, analyses, cacheOptions);
                _logger.LogInformation("Cached {Count} saved analyses for {Duration}", analyses.Count, CacheDuration);
            }

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
            var response = await _httpClient.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted shared analysis {Id}", id);
                
                // Invalidate cache to force refresh on next request
                _memoryCache.Remove(CacheKey);
                
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
        // Check cache first
        var cacheKey = $"{CacheKeyPrefix}{id}";
        if (_memoryCache.TryGetValue(cacheKey, out AnalysisData? cachedAnalysis))
        {
            _logger.LogInformation($"Retrieved shared analysis {id} from cache");
            return cachedAnalysis;
        }

        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(3);
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation($"Getting shared analysis with ID: {id}, attempt {attempt}");

                var getSharedAnalysisCode = Configuration["FeedbackApi:FunctionsKey"]
                    ?? throw new InvalidOperationException("GetSharedAnalysisCode API code not configured");

                var getSharedPath = $"{BaseUrl}/api/GetSharedAnalysis/{id}?code={Uri.EscapeDataString(getSharedAnalysisCode)}";
                var response = await _httpClient.GetAsync(getSharedPath);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get shared analysis: {response.StatusCode} (attempt {attempt})");
                    if (attempt == maxRetries)
                        return null;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var analysisData = JsonSerializer.Deserialize<AnalysisData>(responseContent, _jsonOptions);

                    // Cache the result
                    if (analysisData != null)
                    {
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(SharedAnalysisCacheDuration)
                            .SetPriority(CacheItemPriority.Normal);
                        _memoryCache.Set(cacheKey, analysisData, cacheOptions);
                        _logger.LogInformation($"Cached shared analysis {id} for {SharedAnalysisCacheDuration}");
                    }

                    return analysisData;
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, $"Error getting shared analysis (attempt {attempt}), will retry");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shared analysis");
                return null;
            }
            await Task.Delay(delay);
            delay = delay * 2;
        }
        return null;
    }

    public async Task RefreshUsersSavedAnalysesAsync()
    {
        _logger.LogInformation("Refreshing user's saved analyses cache");
        
        // Remove from cache to force fresh fetch
        _memoryCache.Remove(CacheKey);
        
        // Fetch fresh data
        await GetUsersSavedAnalysesAsync();
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

    public async Task<string> ShareAnalysisAsync(AnalysisData analysis)
    {
        try
        {
            _logger.LogInformation("Sharing analysis");
            
            var content = new StringContent(
                JsonSerializer.Serialize(analysis, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            
            var saveSharedAnalysisCode = Configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("SaveSharedAnalysisCode API code not configured");

            var saveSharedAnalysisUrl = $"{BaseUrl}/api/SaveSharedAnalysis?code={Uri.EscapeDataString(saveSharedAnalysisCode)}";

            var response = await _httpClient.PostAsync(saveSharedAnalysisUrl, content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ShareResponse>(responseContent, _jsonOptions);
            
            if (result == null || string.IsNullOrEmpty(result.Id))
            {
                throw new InvalidOperationException("Invalid response from sharing service");
            }

            // Invalidate cache to include the new analysis
            _memoryCache.Remove(CacheKey);

            return result.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing analysis");
            throw;
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
