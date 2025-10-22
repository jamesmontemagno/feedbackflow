using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharedDump.Models.ContentSearch;

namespace FeedbackFunctions.OmniSearch;

/// <summary>
/// Helper service for searching Twitter/X platform
/// </summary>
public class TwitterSearchHelper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterSearchHelper> _logger;
    private readonly string? _bearerToken;

    public TwitterSearchHelper(HttpClient httpClient, ILogger<TwitterSearchHelper> logger, string? bearerToken)
    {
        _httpClient = httpClient;
        _logger = logger;
        _bearerToken = bearerToken;
    }

    public async Task<List<OmniSearchResult>> SearchAsync(string query, int maxResults, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken cancellationToken)
    {
        var results = new List<OmniSearchResult>();

        if (string.IsNullOrEmpty(_bearerToken))
        {
            _logger.LogWarning("Twitter Bearer Token not configured - skipping Twitter search");
            return results;
        }

        try
        {
            // Build the search URL with parameters
            var escapedQuery = Uri.EscapeDataString(query);
            var searchUrl = $"https://api.twitter.com/2/tweets/search/recent?query={escapedQuery}" +
                           $"&tweet.fields=author_id,created_at,public_metrics" +
                           $"&expansions=author_id" +
                           $"&user.fields=name,username" +
                           $"&max_results={Math.Min(maxResults, 100)}";

            // Add date filter if provided
            if (fromDate.HasValue)
            {
                searchUrl += $"&start_time={fromDate.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            if (toDate.HasValue)
            {
                searchUrl += $"&end_time={toDate.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twitter search failed with status {Status}", response.StatusCode);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse user information
            var userDict = new Dictionary<string, (string name, string username)>();
            if (root.TryGetProperty("includes", out var includes) &&
                includes.TryGetProperty("users", out var users))
            {
                foreach (var user in users.EnumerateArray())
                {
                    var userId = user.GetProperty("id").GetString() ?? "";
                    var name = user.GetProperty("name").GetString() ?? "";
                    var username = user.GetProperty("username").GetString() ?? "";
                    userDict[userId] = (name, username);
                }
            }

            // Parse tweet data
            if (root.TryGetProperty("data", out var data))
            {
                foreach (var tweet in data.EnumerateArray())
                {
                    var tweetId = tweet.GetProperty("id").GetString() ?? "";
                    var text = tweet.GetProperty("text").GetString() ?? "";
                    var authorId = tweet.GetProperty("author_id").GetString() ?? "";
                    var createdAt = tweet.GetProperty("created_at").GetString() ?? "";

                    var (authorName, authorUsername) = userDict.GetValueOrDefault(authorId, ("Unknown", "unknown"));
                    
                    var engagementCount = 0L;
                    if (tweet.TryGetProperty("public_metrics", out var metrics))
                    {
                        engagementCount = (metrics.TryGetProperty("like_count", out var likes) ? likes.GetInt64() : 0) +
                                        (metrics.TryGetProperty("retweet_count", out var retweets) ? retweets.GetInt64() : 0) +
                                        (metrics.TryGetProperty("reply_count", out var replies) ? replies.GetInt64() : 0);
                    }

                    results.Add(new OmniSearchResult
                    {
                        Id = $"twitter_{tweetId}",
                        Title = $"@{authorUsername}: {TruncateText(text, 100)}",
                        Snippet = TruncateText(text, 200),
                        Source = "Twitter",
                        SourceId = tweetId,
                        Url = $"https://twitter.com/{authorUsername}/status/{tweetId}",
                        PublishedAt = DateTimeOffset.TryParse(createdAt, out var dt) ? dt : DateTimeOffset.UtcNow,
                        Author = $"@{authorUsername}",
                        EngagementCount = engagementCount
                    });
                }
            }

            _logger.LogInformation("Found {Count} Twitter results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Twitter");
            return results;
        }
    }

    private string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
