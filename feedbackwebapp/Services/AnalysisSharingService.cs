using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;
using Microsoft.JSInterop;
using SharedDump.Models;

namespace FeedbackWebApp.Services;

public class AnalysisSharingService : IAnalysisSharingService, IDisposable
{
    private const string SharedHistoryStorageKey = "feedbackflow_shared_analysis_history";
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AnalysisSharingService> _logger;
    private readonly List<SharedAnalysisRecord> _inMemorySharedHistory;
    private bool _initialized;
    private readonly string _shareFunctionUrl;
    private readonly string _sharingBaseUrl;

    public AnalysisSharingService(
        IHttpClientFactory httpClientFactory, 
        IJSRuntime jsRuntime, 
        ILogger<AnalysisSharingService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient("DefaultClient");
        _jsRuntime = jsRuntime;
        _logger = logger;
        _inMemorySharedHistory = new();

        // Get the function URL from configuration
        _shareFunctionUrl = configuration.GetValue<string>("ShareFunctionUrl") ?? 
            "https://feedbackflow.azurewebsites.net/api/SaveSharedAnalysis";

        // Get the base URL for sharing
        _sharingBaseUrl = configuration.GetValue<string>("SharingBaseUrl") ?? 
            "https://feedbackflow.azurewebsites.net/shared";
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SharedHistoryStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var history = JsonSerializer.Deserialize<List<SharedAnalysisRecord>>(json);
                if (history != null)
                {
                    _inMemorySharedHistory.Clear();
                    _inMemorySharedHistory.AddRange(history);
                }
            }
            _initialized = true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error initializing shared history");
            // We're probably in prerendering, return empty list
            _initialized = false;
        }
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
            
            var response = await _httpClient.GetAsync($"{_sharingBaseUrl}/shared/{id}");
            
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

    public async Task<List<SharedAnalysisRecord>> GetSharedAnalysisHistoryAsync()
    {
        await InitializeAsync();
        return new List<SharedAnalysisRecord>(_inMemorySharedHistory);
    }

    public async Task SaveSharedAnalysisToHistoryAsync(SharedAnalysisRecord record)
    {
        await InitializeAsync();
        _inMemorySharedHistory.RemoveAll(x => x.Id == record.Id);
        _inMemorySharedHistory.Insert(0, record); // Most recent first

        try
        {
            var json = JsonSerializer.Serialize(_inMemorySharedHistory);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SharedHistoryStorageKey, json);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error saving shared history");
            // We're probably in prerendering, just keep in memory
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