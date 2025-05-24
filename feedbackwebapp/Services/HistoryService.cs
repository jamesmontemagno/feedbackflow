using Microsoft.JSInterop;
using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services;

public class HistoryService : IHistoryService, IDisposable
{    private readonly IJSRuntime _jsRuntime;
    private readonly List<AnalysisHistoryItem> _inMemoryHistory;
    private const string StorageKey = "feedbackflow_analysis_history";
    private bool _initialized;

    public HistoryService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _inMemoryHistory = new();
    }

    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", cancellationToken, StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var history = System.Text.Json.JsonSerializer.Deserialize<List<AnalysisHistoryItem>>(json);
                if (history != null)
                {
                    _inMemoryHistory.Clear();
                    _inMemoryHistory.AddRange(history);
                }
            }
            _initialized = true;
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering, return empty list
            _initialized = false;
        }
    }

    public async Task<List<AnalysisHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return new List<AnalysisHistoryItem>(_inMemoryHistory);
    }

    public async Task SaveToHistoryAsync(AnalysisHistoryItem item, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        _inMemoryHistory.RemoveAll(x => x.Id == item.Id);
        _inMemoryHistory.Insert(0, item); // Most recent first

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_inMemoryHistory);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering, just keep in memory
        }
    }

    public async Task DeleteHistoryItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();
        _inMemoryHistory.RemoveAll(x => x.Id == id);

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_inMemoryHistory);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering, just keep in memory
        }
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        _inMemoryHistory.Clear();
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering, just keep in memory
        }
        _initialized = false;
    }

    public void Dispose()
    {
        // Nothing to dispose
        GC.SuppressFinalize(this);
    }
}
