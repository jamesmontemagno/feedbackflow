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
        if (_initialized)
            return;

        try
        {
            _initialized = true;
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/indexedDb.js");
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
        // No longer saving to IndexedDB - this is now a no-op
        // History items are managed through the cloud service only
        await Task.CompletedTask;
    }

    public async Task<List<AnalysisHistoryItem>> GetHistoryPagedAsync(int skip, int take, string? searchTerm = null)
    {
        await InitializeAsync();
        if (_module == null) return new List<AnalysisHistoryItem>();

        try
        {
            var items = await _module.InvokeAsync<List<AnalysisHistoryItem>>("getHistoryItemsPaged", skip, take, searchTerm);
            return items.OrderByDescending(x => x.Timestamp).ToList(); // Most recent first
        }
        catch (InvalidOperationException)
        {
            return new List<AnalysisHistoryItem>();
        }
    }

    public async Task<int> GetHistoryCountAsync(string? searchTerm = null)
    {
        await InitializeAsync();
        if (_module == null) return 0;

        try
        {
            return await _module.InvokeAsync<int>("getHistoryItemsCount", searchTerm);
        }
        catch (InvalidOperationException)
        {
            return 0;
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
