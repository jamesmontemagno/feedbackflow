using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace FeedbackWebApp.Components.Shared;

public class CollapsibleSectionBase : ComponentBase, IDisposable
{
    private DotNetObjectReference<CollapsibleSectionBase>? _selfRef;
    private bool _initialized;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string? SecondaryText { get; set; }
    [Parameter] public string SectionId { get; set; } = Guid.NewGuid().ToString("N");
    [Parameter] public bool DefaultOpen { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected string HeaderId => $"collapsible-header-{SectionId}";
    protected string ContentId => $"collapsible-content-{SectionId}";
    protected bool IsOpen { get; private set; }

    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected ILogger<CollapsibleSectionBase> Logger { get; set; } = default!;

    protected override void OnInitialized() => IsOpen = DefaultOpen;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;
            _selfRef = DotNetObjectReference.Create(this);
            try
            {
                var stored = await JSRuntime.InvokeAsync<string?>("collapsibleSection.get", SectionId);
                if (!string.IsNullOrEmpty(stored) && bool.TryParse(stored, out var open))
                {
                    IsOpen = open;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to restore collapsible section state {SectionId}", SectionId);
            }
        }
    }

    protected async Task Toggle()
    {
        IsOpen = !IsOpen;
        StateHasChanged();
        try
        {
            await JSRuntime.InvokeVoidAsync("collapsibleSection.set", SectionId, IsOpen);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to persist collapsible section state");
        }
    }

    public void Dispose()
    {
        _selfRef?.Dispose();
    }
}
