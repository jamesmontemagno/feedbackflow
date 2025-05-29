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

    private async Task MigrateFromLocalStorageAsync()
    {
        try
        {
            // Get the data from localStorage
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return;

            // Parse the JSON into our model
            var items = System.Text.Json.JsonSerializer.Deserialize<List<AnalysisHistoryItem>>(json);
            if (items == null || !items.Any())
                return;

            // Save each item to IndexedDB
            foreach (var item in items)
            {
                await _module!.InvokeVoidAsync("saveHistoryItem", item);
            }

            // Clear the localStorage
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (Exception)
        {
            // If anything fails during migration, we'll just continue without migrating
        }
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            _initialized = true;
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/indexedDb.js");
            // After initializing IndexedDB, try to migrate any existing data
            await MigrateFromLocalStorageAsync();
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
            _initialized = false;
        }
    }

    public async Task<List<AnalysisHistoryItem>> GetHistoryAsync()
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

    public async Task UpdateHistoryItemAsync(AnalysisHistoryItem item)
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("updateHistoryItem", item);
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit is already disconnected, no need to dispose JS module
            }
        }

        GC.SuppressFinalize(this);
    }
}
