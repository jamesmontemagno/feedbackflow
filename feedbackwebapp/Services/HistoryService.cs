using Microsoft.JSInterop;
using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services;

public class HistoryService : IHistoryService, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "feedbackflow_analysis_history";
    private bool _disposed;

    public HistoryService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<AnalysisHistoryItem>> GetHistoryAsync()
    {
        var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
        if (string.IsNullOrEmpty(json))
            return new List<AnalysisHistoryItem>();
        return System.Text.Json.JsonSerializer.Deserialize<List<AnalysisHistoryItem>>(json) ?? new List<AnalysisHistoryItem>();
    }

    public async Task SaveToHistoryAsync(AnalysisHistoryItem item)
    {
        var history = await GetHistoryAsync();
        history.RemoveAll(x => x.Id == item.Id);
        history.Insert(0, item); // Most recent first
        var json = System.Text.Json.JsonSerializer.Serialize(history);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task DeleteHistoryItemAsync(string id)
    {
        var history = await GetHistoryAsync();
        history.RemoveAll(x => x.Id == id);
        var json = System.Text.Json.JsonSerializer.Serialize(history);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task ClearHistoryAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
