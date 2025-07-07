using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SharedDump.Utils;

public static class RedditUrlParser
{
    private static readonly Regex ThreadRegex = new(
        @"(?:https?://)?(?:www\.)?reddit\.com/r/[^/]+/comments/([a-z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortlinkRegex = new(
        @"(?:https?://)?(?:www\.)?reddit\.com/r/[^/]+/s/([a-zA-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);



    public static string? ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = ThreadRegex.Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }

    public static bool IsRedditShortUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) && ShortlinkRegex.IsMatch(url);

    public static async Task<string?> GetShortlinkIdAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsRedditShortUrl(url))
            return null;
        try
        {
            // Allow redirects and set up the client with browser-like headers
            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = true;

            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                if (finalUrl != null)
                {
                    Console.WriteLine($"Original URL: {url}");
                    Console.WriteLine($"Full URL: {finalUrl}");

                    // Extract post ID
                    var postIdMatch = finalUrl.Split("/comments/").ElementAtOrDefault(1)?.Split('/').FirstOrDefault();
                    if (postIdMatch != null)
                    {
                        Console.WriteLine($"Post ID: {postIdMatch}");
                        return postIdMatch;
                    }
                }
            }
            else
            {
                Console.WriteLine($"Request failed with status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing URL: {ex.Message}");
        }
        return null;
    }

    public static async Task<string?> GetShortlinkIdAsync2(string url, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsRedditShortUrl(url))
            return null;
        
        try
        {
            // Extract the shortlink identifier from the URL
            var match = ShortlinkRegex.Match(url);
            if (!match.Success || match.Groups.Count < 2)
                return null;
                
            var shortId = match.Groups[1].Value;
            
            // Configure HttpClientHandler with specific settings for Reddit
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false  // Avoiding cookies can help with some blocking issues
            };

            using var client = new HttpClient(handler)
            {
                // Shorter timeout as we only need the redirect
                Timeout = TimeSpan.FromSeconds(10)
            };
            
            // More complete browser-like headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            client.DefaultRequestHeaders.Add("Pragma", "no-cache");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            
            // Use a HEAD request instead of GET - this is less resource-intensive 
            // and less likely to be blocked, as we only need the redirect URL
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                if (finalUrl != null)
                {
                    logger?.LogInformation("Original URL: {OriginalUrl}", url);
                    logger?.LogInformation("Final URL: {FinalUrl}", finalUrl);

                    // Extract post ID
                    var postIdMatch = finalUrl.Split("/comments/").ElementAtOrDefault(1)?.Split('/').FirstOrDefault();
                    if (postIdMatch != null)
                    {
                        logger?.LogInformation("Post ID: {PostId}", postIdMatch);
                        return postIdMatch;
                    }
                }
            }
            else
            {
                logger?.LogWarning("Request failed with status code: {StatusCode}", response.StatusCode);
                
                // Fallback for blocked redirects - try to resolve it through the Reddit API if available
                // This would require authentication with the Reddit API
                // You might want to implement this if the direct approach keeps failing
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error processing Reddit shortlink URL: {Url}", url);
        }
        
        return null;
    }


    public static IEnumerable<string> ParseMultipleIds(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        // Split by commas and process each part
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();

            // Check if it's a URL
            var threadId = ParseUrl(trimmed);
            if (threadId != null)
            {
                yield return threadId;
                continue;
            }

            // If it's not a URL, check if it's a direct thread ID (alphanumeric)
            if (Regex.IsMatch(trimmed, "^[a-z0-9]+$", RegexOptions.IgnoreCase))
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>
    /// Generates a properly formatted Reddit comment URL
    /// </summary>
    /// <param name="threadPermalink">The thread permalink (e.g., "/r/subreddit/comments/threadid/title/")</param>
    /// <param name="commentId">The comment ID</param>
    /// <returns>A properly formatted Reddit comment URL</returns>
    public static string GenerateCommentUrl(string threadPermalink, string commentId)
    {
        if (string.IsNullOrWhiteSpace(threadPermalink) || string.IsNullOrWhiteSpace(commentId))
            return string.Empty;

        // Ensure permalink starts with /
        if (!threadPermalink.StartsWith('/'))
        {
            threadPermalink = "/" + threadPermalink;
        }
        
        // Ensure permalink ends with /
        if (!threadPermalink.EndsWith('/'))
        {
            threadPermalink += "/";
        }
        
        // Construct the full URL
        return $"https://www.reddit.com{threadPermalink}{commentId}/";
    }
}