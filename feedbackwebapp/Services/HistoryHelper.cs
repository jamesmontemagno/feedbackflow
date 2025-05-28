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
    }

    public string GetSourceUrl(AnalysisHistoryItem item)
    {
        if (string.IsNullOrEmpty(item.UserInput) || string.IsNullOrEmpty(item.SourceType))
            return "#";

        var id = item.UserInput;
        var sourceType = item.SourceType.ToLowerInvariant();

        // First check if the input is already a valid URL for the source type
        var isValidUrl = sourceType switch
        {
            "youtube" => UrlParsing.IsYouTubeUrl(id),
            "github" => UrlParsing.IsGitHubUrl(id),
            "reddit" => UrlParsing.IsRedditUrl(id),
            "twitter" => UrlParsing.IsTwitterUrl(id),
            "hackernews" => UrlParsing.IsHackerNewsUrl(id),
            "devblogs" => DevBlogsUrlValidator.IsValidDevBlogsUrl(id),
            "bluesky" => UrlParsing.IsBlueSkyUrl(id),
            _ => false
        };

        if (isValidUrl)
            return id;

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
            "bluesky" => "bi-cloud",
            _ => "bi-question-circle"
        };
    }

    public string ConvertMarkdownToHtml(string? markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    }
}
