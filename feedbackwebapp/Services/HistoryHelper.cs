using Markdig;
using Microsoft.JSInterop;
using SharedDump.Models;
using SharedDump.Utils;

namespace FeedbackWebApp.Services;

public interface IHistoryHelper
{
    Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService);
    Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService);
    Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService, Action<string, string>? onCopyFailed);
    Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService, Action<string, string>? onCopyFailed);
    string GetSourceUrl(AnalysisHistoryItem item);
    string GetServiceIcon(string sourceType);
    string ConvertMarkdownToHtml(string? markdown);
}

public class HistoryHelper : IHistoryHelper
{
    public async Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService)
    {
        await CopyShareLink(item, baseUri, jsRuntime, toastService, null);
    }
    
    public async Task CopyShareLink(AnalysisHistoryItem item, string baseUri, IJSRuntime jsRuntime, IToastService toastService, Action<string, string>? onCopyFailed)
    {
        if (item.IsShared && !string.IsNullOrEmpty(item.SharedId))
        {
            var baseUrl = baseUri.TrimEnd('/');
            var shareLink = $"{baseUrl}/analysis/{item.SharedId}";
            
            try
            {
                var success = await jsRuntime.InvokeAsync<bool>("copyToClipboard", shareLink);
                if (success)
                {
                    await toastService.ShowSuccessAsync("Share link copied to clipboard!", 3000);
                }
                else
                {
                    if (onCopyFailed != null)
                    {
                        onCopyFailed(shareLink, "Share Link");
                    }
                    else
                    {
                        await toastService.ShowErrorAsync("Failed to copy share link. Please try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (onCopyFailed != null)
                {
                    onCopyFailed(shareLink, "Share Link");
                }
                else
                {
                    await toastService.ShowErrorAsync($"Failed to copy share link: {ex.Message}");
                }
            }
        }
    }

    public async Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService)
    {
        await CopyToClipboard(item, jsRuntime, toastService, null);
    }

    public async Task CopyToClipboard(AnalysisHistoryItem item, IJSRuntime jsRuntime, IToastService toastService, Action<string, string>? onCopyFailed)
    {
        try
        {
            string textToCopy = "";
            string successMessage = "";
            string contentType = "";
            
            if (!string.IsNullOrEmpty(item.FullAnalysis))
            {
                textToCopy = item.FullAnalysis;
                successMessage = "Analysis copied to clipboard";
                contentType = "Analysis";
            }
            else if (!string.IsNullOrEmpty(item.Summary))
            {
                textToCopy = item.Summary;
                successMessage = "Summary copied to clipboard";
                contentType = "Summary";
            }
            else
            {
                await toastService.ShowErrorAsync("Nothing to copy - no analysis or summary available.");
                return;
            }
            
            var success = await jsRuntime.InvokeAsync<bool>("copyToClipboard", textToCopy);
            if (success)
            {
                await toastService.ShowSuccessAsync(successMessage, 3000);
            }
            else
            {
                if (onCopyFailed != null)
                {
                    onCopyFailed(textToCopy, contentType);
                }
                else
                {
                    await toastService.ShowErrorAsync("Failed to copy to clipboard. Please try again.");
                }
            }
        }
        catch (Exception ex)
        {
            string fallbackContent = item.FullAnalysis ?? item.Summary ?? "";
            string fallbackType = !string.IsNullOrEmpty(item.FullAnalysis) ? "Analysis" : "Summary";
            
            if (onCopyFailed != null && !string.IsNullOrEmpty(fallbackContent))
            {
                onCopyFailed(fallbackContent, fallbackType);
            }
            else
            {
                await toastService.ShowErrorAsync($"Failed to copy to clipboard: {ex.Message}");
            }
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
    }    public string ConvertMarkdownToHtml(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;
            
        // Increase header levels by 2 for better preview display
        var adjustedMarkdown = AdjustHeaderLevels(markdown, 2);
        return Markdown.ToHtml(adjustedMarkdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    }
    
    /// <summary>
    /// Adjusts markdown header levels by adding the specified increment
    /// </summary>
    /// <param name="markdown">The markdown content</param>
    /// <param name="increment">Number of levels to add to headers (e.g., 2 to change # to ###)</param>
    /// <returns>Markdown with adjusted header levels</returns>
    private static string AdjustHeaderLevels(string markdown, int increment)
    {
        if (increment <= 0) return markdown;
        
        var lines = markdown.Split('\n');
        var adjustedLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();
            
            // Check if line starts with markdown header syntax
            if (trimmedLine.StartsWith('#') && trimmedLine.Length > 0)
            {
                // Count existing hash marks
                int hashCount = 0;
                foreach (char c in trimmedLine)
                {
                    if (c == '#')
                        hashCount++;
                    else
                        break;
                }
                
                // Make sure we found a proper header (has space or end after hashes)
                if (hashCount > 0 && (hashCount == trimmedLine.Length || trimmedLine[hashCount] == ' '))
                {
                    // Ensure we don't exceed 6 header levels (markdown max)
                    int newHashCount = Math.Min(6, hashCount + increment);
                    
                    // Extract the text after the hashes
                    var headerText = trimmedLine.Substring(hashCount).TrimStart();
                    
                    // Reconstruct with new hash count, preserving original indentation
                    var indentation = line.Substring(0, line.Length - trimmedLine.Length);
                    var newLine = indentation + new string('#', newHashCount) + " " + headerText;
                    adjustedLines.Add(newLine);
                }
                else
                {
                    // Not a proper header, keep as is
                    adjustedLines.Add(line);
                }
            }
            else
            {
                adjustedLines.Add(line);
            }
        }
        
        return string.Join('\n', adjustedLines);
    }
}
