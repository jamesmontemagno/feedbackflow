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
    private readonly string _shareFunctionUrl;
    private readonly string _sharingBaseUrl;

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
        var baseUrl = configuration.GetValue<string>("FeedbackApi:BaseUrl") ?? 
            "https://feedbackflow.azurewebsites.net";
        var saveSharedAnalysisCode = configuration.GetValue<string>("FeedbackApi:SaveSharedAnalysisCode") ?? 
            "api/SaveSharedAnalysis";

        // Build function URLs
        _shareFunctionUrl = $"{baseUrl.TrimEnd('/')}/{saveSharedAnalysisCode.TrimStart('/')}";
        _sharingBaseUrl = baseUrl.TrimEnd('/');
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

            var response = await _httpClient.PostAsync(_shareFunctionUrl, content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ShareResponse>(responseContent);
            
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
        try
        {
            _logger.LogInformation($"Getting shared analysis with ID: {id}");
            
            var getSharedPath = $"{_sharingBaseUrl}/api/GetSharedAnalysis/{id}";
            var response = await _httpClient.GetAsync(getSharedPath);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get shared analysis: {response.StatusCode}");
                return null;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var analysisData = JsonSerializer.Deserialize<AnalysisData>(responseContent);
            
            return analysisData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shared analysis");
            return null;
        }
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