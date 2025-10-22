using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharedDump.Models.ContentSearch;

namespace FeedbackFunctions.OmniSearch;

/// <summary>
/// Helper service for searching BlueSky platform
/// </summary>
public class BlueSkySearchHelper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueSkySearchHelper> _logger;
    private readonly string? _username;
    private readonly string? _appPassword;
    private string? _accessJwt;
    private bool _isAuthenticated;

    public BlueSkySearchHelper(HttpClient httpClient, ILogger<BlueSkySearchHelper> logger, string? username, string? appPassword)
    {
        _httpClient = httpClient;
        _logger = logger;
        _username = username;
        _appPassword = appPassword;
    }

    private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (_isAuthenticated && !string.IsNullOrEmpty(_accessJwt))
            return true;

        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_appPassword))
        {
            _logger.LogWarning("BlueSky credentials not configured - skipping BlueSky search");
            return false;
        }

        try
        {
            var authUrl = "https://bsky.social/xrpc/com.atproto.server.createSession";
            var authPayload = JsonSerializer.Serialize(new
            {
                identifier = _username,
                password = _appPassword
            });

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = new StringContent(authPayload, System.Text.Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to authenticate with BlueSky API: {Status}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            _accessJwt = doc.RootElement.GetProperty("accessJwt").GetString();
            _isAuthenticated = true;

            _logger.LogInformation("Successfully authenticated with BlueSky API");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with BlueSky API");
            return false;
        }
    }

    public async Task<List<OmniSearchResult>> SearchAsync(string query, int maxResults, DateTimeOffset? fromDate, CancellationToken cancellationToken)
    {
        var results = new List<OmniSearchResult>();

        // Authenticate first
        if (!await AuthenticateAsync(cancellationToken))
        {
            return results;
        }

        try
        {
            // BlueSky search endpoint: /xrpc/app.bsky.feed.searchPosts
            // Can be called on public API without auth, but auth gives better results
            var escapedQuery = Uri.EscapeDataString(query);
            var searchUrl = $"https://public.api.bsky.app/xrpc/app.bsky.feed.searchPosts?q={escapedQuery}&limit={Math.Min(maxResults, 100)}";

            // Add sort parameter (optional: top, latest)
            searchUrl += "&sort=latest";

            // Note: BlueSky doesn't have native date filtering in the search API
            // We'll filter results after fetching

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            
            // Add auth header if available (gives better results)
            if (!string.IsNullOrEmpty(_accessJwt))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BlueSky search failed with status {Status}", response.StatusCode);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse post data
            if (root.TryGetProperty("posts", out var posts))
            {
                foreach (var post in posts.EnumerateArray())
                {
                    try
                    {
                        var uri = post.GetProperty("uri").GetString() ?? "";
                        var cid = post.GetProperty("cid").GetString() ?? "";
                        
                        var record = post.GetProperty("record");
                        var text = record.GetProperty("text").GetString() ?? "";
                        var createdAt = record.GetProperty("createdAt").GetString() ?? "";
                        
                        var author = post.GetProperty("author");
                        var authorHandle = author.GetProperty("handle").GetString() ?? "";
                        var authorDisplayName = author.TryGetProperty("displayName", out var displayName) 
                            ? displayName.GetString() ?? authorHandle 
                            : authorHandle;

                        var publishedAt = DateTimeOffset.TryParse(createdAt, out var dt) ? dt : DateTimeOffset.UtcNow;

                        // Apply date filter if specified
                        if (fromDate.HasValue && publishedAt < fromDate.Value)
                            continue;

                        var engagementCount = 0L;
                        if (post.TryGetProperty("likeCount", out var likes))
                            engagementCount += likes.GetInt64();
                        if (post.TryGetProperty("replyCount", out var replies))
                            engagementCount += replies.GetInt64();
                        if (post.TryGetProperty("repostCount", out var reposts))
                            engagementCount += reposts.GetInt64();

                        // Extract post ID from URI (format: at://did:plc:xxx/app.bsky.feed.post/xxxxx)
                        var postId = uri.Split('/').LastOrDefault() ?? cid;
                        var postUrl = $"https://bsky.app/profile/{authorHandle}/post/{postId}";

                        results.Add(new OmniSearchResult
                        {
                            Id = $"bluesky_{postId}",
                            Title = $"{authorDisplayName} (@{authorHandle}): {TruncateText(text, 100)}",
                            Snippet = TruncateText(text, 200),
                            Source = "BlueSky",
                            SourceId = postId,
                            Url = postUrl,
                            PublishedAt = publishedAt,
                            Author = $"@{authorHandle}",
                            EngagementCount = engagementCount
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing BlueSky post");
                        continue;
                    }
                }
            }

            _logger.LogInformation("Found {Count} BlueSky results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching BlueSky");
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
