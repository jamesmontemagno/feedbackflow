using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace FeedbackWebApp.Services;

public static class ToastServiceExtensions
{
    /// <summary>
    /// Shows a success toast notification
    /// </summary>
    public static Task ShowSuccessAsync(this IToastService toastService, string message, int durationMs = 3000, ToastPosition position = ToastPosition.BottomEnd)
        => toastService.ShowToastAsync(message, ToastType.Success, durationMs, position);
    
    /// <summary>
    /// Shows a warning toast notification
    /// </summary>
    public static Task ShowWarningAsync(this IToastService toastService, string message, int durationMs = 3000, ToastPosition position = ToastPosition.BottomEnd)
        => toastService.ShowToastAsync(message, ToastType.Warning, durationMs, position);
    
    /// <summary>
    /// Shows an error toast notification
    /// </summary>
    public static Task ShowErrorAsync(this IToastService toastService, string message, int durationMs = 4000, ToastPosition position = ToastPosition.BottomEnd)
        => toastService.ShowToastAsync(message, ToastType.Danger, durationMs, position);
    
    /// <summary>
    /// Shows an info toast notification
    /// </summary>
    public static Task ShowInfoAsync(this IToastService toastService, string message, int durationMs = 3000, ToastPosition position = ToastPosition.BottomEnd)
        => toastService.ShowToastAsync(message, ToastType.Info, durationMs, position);
}
