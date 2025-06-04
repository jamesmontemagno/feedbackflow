using Markdig;
using Microsoft.JSInterop;
using SharedDump.Models;
using SharedDump.Utils;

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
    }    public string GetSourceUrl(AnalysisHistoryItem item)
    {
        if (string.IsNullOrEmpty(item.UserInput) || string.IsNullOrEmpty(item.SourceType))
            return "#";

        // Handle multiple comma-delimited URLs
        var urls = item.UserInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var url in urls)
        {
            if (UrlParsing.IsValidUrl(url))
                return url;
        }

        // If no valid URL found, use the first input as ID
        var id = urls.FirstOrDefault() ?? item.UserInput;
        
        // Use the first source type for URL construction
        var sourceTypes = item.SourceType.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sourceType = sourceTypes.FirstOrDefault()?.ToLowerInvariant() ?? "";

        // If not a valid URL, construct one from the ID
        return sourceType switch
        {
            "youtube" => $"https://youtube.com/watch?v={id}",
            "github" => $"https://github.com/{id}",
            "reddit" => $"https://reddit.com/{id}",
            "twitter" => $"https://twitter.com/i/web/status/{id}",
            "hackernews" => $"https://news.ycombinator.com/item?id={id}",
            "devblogs" => $"https://devblogs.microsoft.com/{id}",
            "bluesky" => $"https://bsky.app/profile/{id}",
            _ => "#"
        };
    }public string GetServiceIcon(string sourceType)
    {
        return ServiceIconHelper.GetServiceIcon(sourceType);
    }public string ConvertMarkdownToHtml(string? markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    }
}
