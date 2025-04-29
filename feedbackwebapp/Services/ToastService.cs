using Microsoft.JSInterop;

namespace FeedbackWebApp.Services;

public enum ToastType
{
    Success,
    Warning,
    Danger,
    Info,
    Primary
}

public enum ToastPosition
{
    TopStart,
    TopCenter,
    TopEnd,
    MiddleStart,
    MiddleCenter,
    MiddleEnd,
    BottomStart,
    BottomCenter,
    BottomEnd
}

public interface IToastService
{
    Task ShowToastAsync(string message, ToastType type = ToastType.Success, int durationMs = 3000, ToastPosition position = ToastPosition.BottomEnd);
}

public class ToastService : IToastService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _jsModule;
    private bool _disposed;

    public ToastService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task<IJSObjectReference> GetJsModuleAsync()
    {
        if (_jsModule == null)
        {
            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/toastService.js");
        }

        return _jsModule;
    }

    public async Task ShowToastAsync(string message, ToastType type = ToastType.Success, int durationMs = 3000, ToastPosition position = ToastPosition.BottomEnd)
    {
        var module = await GetJsModuleAsync();
        var positionStr = GetPositionString(position);
        await module.InvokeVoidAsync("showToast", message, type.ToString().ToLowerInvariant(), durationMs, positionStr);
    }
    
    private static string GetPositionString(ToastPosition position) => position switch
    {
        ToastPosition.TopStart => "top-start",
        ToastPosition.TopCenter => "top-center",
        ToastPosition.TopEnd => "top-end",
        ToastPosition.MiddleStart => "middle-start",
        ToastPosition.MiddleCenter => "middle-center",
        ToastPosition.MiddleEnd => "middle-end",
        ToastPosition.BottomStart => "bottom-start",
        ToastPosition.BottomCenter => "bottom-center",
        ToastPosition.BottomEnd => "bottom-end",
        _ => "bottom-end"
    };
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_jsModule is not null)
        {
            try
            {

                await _jsModule.DisposeAsync();
                _jsModule = null;
            }
            catch
            {
            }
        }

        _disposed = true;
    }
}
