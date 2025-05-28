using Microsoft.JSInterop;
using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services;

public class HistoryService : IHistoryService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private bool _initialized;
    private const string StorageKey = "feedbackflow_analysis_history";
    public HistoryService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/indexedDb.js");
            // Try to migrate data from localStorage if it exists
            await _module.InvokeVoidAsync("migrateFromLocalStorage", StorageKey);
            _initialized = true;
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
            _initialized = false;
        }
    }    public async Task<List<AnalysisHistoryItem>> GetHistoryAsync()
    {
        await InitializeAsync();
        if (_module == null) return new List<AnalysisHistoryItem>();

        try
        {
            var items = await _module.InvokeAsync<List<AnalysisHistoryItem>>("getAllHistoryItems");
            return items.OrderByDescending(x => x.Timestamp).ToList(); // Most recent first
        }
        catch (InvalidOperationException)
        {
            return new List<AnalysisHistoryItem>();
        }
    }

    public async Task SaveToHistoryAsync(AnalysisHistoryItem item)
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("saveHistoryItem", item);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
        }
    }

    public async Task DeleteHistoryItemAsync(string id)
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("deleteHistoryItem", id);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
        }
    }

    public async Task ClearHistoryAsync()
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("clearHistory");
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
        }
        _initialized = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
