using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;
using Microsoft.JSInterop;
using SharedDump.Models;

namespace FeedbackWebApp.Services;

public class AnalysisSharingService : IAnalysisSharingService, IDisposable
{
    private const string StorageKey = "feedbackflow_analysis_history";
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AnalysisSharingService> _logger;
    private readonly IHistoryService _historyService;
    protected readonly string BaseUrl;
    protected readonly IConfiguration Configuration;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AnalysisSharingService(
        IHttpClientFactory httpClientFactory, 
        IJSRuntime jsRuntime, 
        ILogger<AnalysisSharingService> logger,
        IConfiguration configuration,
        IHistoryService historyService)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _jsRuntime = jsRuntime;
        _logger = logger;
        _historyService = historyService;

        // Get base URLs from configuration
        Configuration = configuration;
        BaseUrl = Configuration?["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    public async Task<string> ShareAnalysisAsync(AnalysisData analysis)
    {
        try
        {
            _logger.LogInformation("Sharing analysis");
            
            var content = new StringContent(
                JsonSerializer.Serialize(analysis),
                Encoding.UTF8,
                "application/json");

            
            var saveSharedAnalysisCode = Configuration["FeedbackApi:SaveSharedAnalysisCode"]
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

            return result.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing analysis");
            throw;
        }
    }

    public async Task<AnalysisData?> GetSharedAnalysisAsync(string id)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(3);
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation($"Getting shared analysis with ID: {id}, attempt {attempt}");

                var getSharedAnalysisCode = Configuration["FeedbackApi:GetSharedAnalysisCode"]
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

    public async Task<List<AnalysisHistoryItem>> GetSharedAnalysisHistoryAsync()
    {
        var allHistory = await _historyService.GetHistoryAsync();
        return allHistory.Where(item => item.IsShared).ToList();
    }

    public async Task UpdateHistoryItemWithShareInfoAsync(string historyItemId, string sharedId)
    {
        try
        {
            var historyItems = await _historyService.GetHistoryAsync();
            var item = historyItems.FirstOrDefault(i => i.Id == historyItemId);
            
            if (item == null)
            {
                _logger.LogWarning($"Could not find history item with ID {historyItemId} to update share status");
                return;
            }
            
            // Create a new item with the shared information
            var updatedItem = item with 
            { 
                IsShared = true,
                SharedId = sharedId,
                SharedDate = DateTime.UtcNow
            };
            
            // Save the updated item to history
            await _historyService.SaveToHistoryAsync(updatedItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating history item with share info: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Clean up any resources
    }

    private class ShareResponse
    {
        public string Id { get; set; } = string.Empty;
    }
}