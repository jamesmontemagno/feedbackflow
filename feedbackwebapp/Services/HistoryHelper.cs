using Markdig;
using Microsoft.JSInterop;
using SharedDump.Models;

namespace FeedbackWebApp.Services;

public interface IHistoryHelper
{
    Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService);
    Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService);
    string GetSourceUrl(AnalysisHistoryItem item);
    string GetServiceIcon(string sourceType);
    string ConvertMarkdownToHtml(string? markdown);
}

public class HistoryHelper : IHistoryHelper
{
    public async Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService)
    {
        if (item.IsShared && !string.IsNullOrEmpty(item.SharedId))
        {
            var baseUrl = baseUri.TrimEnd('/');
            var shareLink = $"{baseUrl}/shared/{item.SharedId}";
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", shareLink);
            await toastService.ShowSuccessAsync("Share link copied to clipboard!", 3000);
        }
    }

    public async Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService)
    {
        if (!string.IsNullOrEmpty(item.FullAnalysis))
        {
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", item.FullAnalysis);
            await toastService.ShowSuccessAsync("Analysis copied to clipboard", 3000);
        }
        else if (!string.IsNullOrEmpty(item.Summary))
        {
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", item.Summary);
            await toastService.ShowSuccessAsync("Summary copied to clipboard", 3000);
        }
    }

    public string GetSourceUrl(AnalysisHistoryItem item)
    {
        return item.SourceType?.ToLowerInvariant() switch
        {
            "youtube" => $"https://youtube.com/watch?v={item.Id}",
            "github" => $"https://github.com/{item.Id}",
            "reddit" => $"https://reddit.com/{item.Id}",
            "twitter" => $"https://twitter.com/i/web/status/{item.Id}",
            "hackernews" => $"https://news.ycombinator.com/item?id={item.Id}",
            "devblogs" => $"https://devblogs.microsoft.com/{item.Id}",
            _ => "#"
        };
    }

    public string GetServiceIcon(string sourceType)
    {
        return sourceType?.ToLowerInvariant() switch
        {
            "youtube" => "bi-youtube",
            "github" => "bi-github",
            "reddit" => "bi-reddit",
            "twitter" => "bi-twitter",
            "hackernews" => "bi-braces",
            "devblogs" => "bi-journal-code",
            "manual" => "bi-pencil-square",
            _ => "bi-question-circle"
        };
    }

    public string ConvertMarkdownToHtml(string? markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    }
}
