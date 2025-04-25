using System.Text.RegularExpressions;

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
}