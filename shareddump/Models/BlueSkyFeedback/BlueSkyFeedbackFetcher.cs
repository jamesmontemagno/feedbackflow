using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharedDump.Json;
using SharedDump.Models.BlueSkyFeedback.ApiModels;
using SharedDump.Models.BlueSkyFeedback.Converters;
using SharedDump.Utils;

namespace SharedDump.Models.BlueSkyFeedback;

/// <summary>
/// Service to fetch and transform BlueSky feedback data.
/// </summary>
public class BlueSkyFeedbackFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlueSkyFeedbackFetcher> _logger;
    private bool _hitRateLimit = false;
    private int _maxReplySearches = 6000; // Limit per 15 minutes, similar to Twitter as a starting point
    private int _currentReplySearches = 0;
    private HashSet<string> _processedPostIds = new();
    
    // Authentication properties
    private string? _accessJwt;
    private string? _refreshJwt;
    private string? _username;
    private string? _appPassword;
    private bool _isAuthenticated = false;

    public BlueSkyFeedbackFetcher(HttpClient httpClient, ILogger<BlueSkyFeedbackFetcher> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Sets the authentication credentials for BlueSky API
    /// </summary>
    public void SetCredentials(string username, string appPassword)
    {
        _username = username;
        _appPassword = appPassword;
        _isAuthenticated = false; // Reset authentication state
    }
    
    /// <summary>
    /// Authenticates with the BlueSky API using the provided credentials
    /// </summary>
    private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        // Skip if already authenticated
        if (_isAuthenticated && !string.IsNullOrEmpty(_accessJwt))
        {
            return true;
        }
        
        // Check if credentials are set
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_appPassword))
        {
            _logger.LogError("BlueSky credentials are not set. Call SetCredentials before using the fetcher.");
            return false;
        }
        
        try
        {
            // Prepare authentication request
            var authRequest = new BlueSkyAuthRequest
            {
                Identifier = _username,
                Password = _appPassword
            };
            
            // BlueSky API endpoint for authentication
            var authUrl = "https://bsky.social/xrpc/com.atproto.server.createSession";
            
            // Send authentication request
            var response = await _httpClient.PostAsJsonAsync(authUrl, authRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to authenticate with BlueSky API: {Status}", response.StatusCode);
                return false;
            }
            
            // Deserialize authentication response
            var authResponse = await response.Content.ReadFromJsonAsync<BlueSkyAuthResponse>(cancellationToken: cancellationToken);
            
            if (authResponse == null || string.IsNullOrEmpty(authResponse.AccessJwt))
            {
                _logger.LogError("Failed to get access token from BlueSky API");
                return false;
            }
            
            // Set authentication properties
            _accessJwt = authResponse.AccessJwt;
            _refreshJwt = authResponse.RefreshJwt;
            
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
    
    /// <summary>
    /// Makes an authenticated GET request to the BlueSky API
    /// </summary>
    private async Task<HttpResponseMessage> GetAuthenticatedAsync(string url, CancellationToken cancellationToken = default)
    {
        // First authenticate if needed
        if (!_isAuthenticated || string.IsNullOrEmpty(_accessJwt))
        {
            var authSuccess = await AuthenticateAsync(cancellationToken);
            if (!authSuccess)
            {
                _logger.LogError("Failed to authenticate before making request to {Url}", url);
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Failed to authenticate with BlueSky API"
                };
            }
        }
        
        // Create a request with authentication header
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
        
        // Send the request
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        // If unauthorized, try to re-authenticate and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Token expired or invalid, trying to re-authenticate");
            _isAuthenticated = false;
            
            var authSuccess = await AuthenticateAsync(cancellationToken);
            if (!authSuccess)
            {
                return response; // Return original unauthorized response
            }
            
            // Retry with new token
            request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
            
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        
        return response;
    }
    
    /// <summary>
    /// Makes an authenticated POST request to the BlueSky API
    /// </summary>
    private async Task<HttpResponseMessage> PostAuthenticatedAsync<T>(string url, T content, CancellationToken cancellationToken = default)
    {
        // First authenticate if needed
        if (!_isAuthenticated || string.IsNullOrEmpty(_accessJwt))
        {
            var authSuccess = await AuthenticateAsync(cancellationToken);
            if (!authSuccess)
            {
                _logger.LogError("Failed to authenticate before making request to {Url}", url);
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Failed to authenticate with BlueSky API"
                };
            }
        }
        
        // Create a request with authentication header
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
        
        // Add content
        var jsonContent = JsonSerializer.Serialize(content);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        // Send the request
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        // If unauthorized, try to re-authenticate and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Token expired or invalid, trying to re-authenticate");
            _isAuthenticated = false;
            
            var authSuccess = await AuthenticateAsync(cancellationToken);
            if (!authSuccess)
            {
                return response; // Return original unauthorized response
            }
            
            // Retry with new token
            request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        
        return response;
    }

    /// <summary>
    /// Fetches feedback from a BlueSky post URL or ID
    /// </summary>
    public async Task<BlueSkyFeedbackResponse?> FetchFeedbackAsync(string postUrlOrId, CancellationToken cancellationToken = default)
    {
        // Reset tracking variables
        _hitRateLimit = false;
        _currentReplySearches = 0;
        _processedPostIds.Clear();
        
        try
        {
            // Authenticate first (this will skip if already authenticated)
            if (!await AuthenticateAsync(cancellationToken))
            {
                _logger.LogError("Failed to authenticate with BlueSky API");
                return null;
            }
            
            var postId = BlueSkyUrlParser.ExtractPostId(postUrlOrId);
            if (string.IsNullOrEmpty(postId))
            {
                _logger.LogError("Invalid post URL or ID: {PostUrlOrId}", postUrlOrId);
                return null;
            }

            postId = postId.Replace("/post/", "/app.bsky.feed.post/");

            // Fetch the main post with more comprehensive information using authenticated request
            var postUrl = $"https://bsky.social/xrpc/app.bsky.feed.getPostThread?uri=at://{postId}";
            var postResp = await GetAuthenticatedAsync(postUrl, cancellationToken);

            if (!postResp.IsSuccessStatusCode)
            {
                // Check for rate limiting
                if ((int)postResp.StatusCode == 429)
                {
                    _hitRateLimit = true;
                    _logger.LogWarning("Rate limit reached when fetching main post {PostId}", postId);
                    return new BlueSkyFeedbackResponse
                    {
                        Items = new List<BlueSkyFeedbackItem>(),
                        MayBeIncomplete = true,
                        RateLimitInfo = "Rate limit reached. BlueSky API rate limits apply."
                    };
                }
                
                _logger.LogError("Failed to fetch post: {Status}", postResp.StatusCode);
                return null;
            }

            // Deserialize the thread response
            var threadResponse = await postResp.Content.ReadFromJsonAsync<BlueSkyThreadResponse>(cancellationToken);
            if (threadResponse?.Thread == null)
            {
                _logger.LogError("Failed to parse thread response for post {PostId}", postId);
                return null;
            }
            
            // Get the full URI which we'll use as a thread ID
            var threadId = threadResponse.Thread.Post?.Uri?.Replace("at://", "") ?? string.Empty;
            if (string.IsNullOrEmpty(threadId))
            {
                _logger.LogError("Missing thread URI in response for post {PostId}", postId);
                return null;
            }

            // Add the main post to processed IDs
            if (!string.IsNullOrEmpty(threadResponse.Thread.Post?.Uri))
            {
                _processedPostIds.Add(threadResponse.Thread.Post.Uri);
            }

            // Process all replies, starting with what's already in the response
            ProcessThreadReplies(threadResponse.Thread);
            
            // We've processed what was directly in the response, convert to our model
            var response = BlueSkyModelConverter.ConvertToFeedbackResponse(
                threadResponse, 
                _hitRateLimit || _currentReplySearches >= _maxReplySearches,
                _processedPostIds.Count,
                (_hitRateLimit || _currentReplySearches >= _maxReplySearches) ? 
                    $"Some replies may be missing due to BlueSky API rate limits. Processed {_processedPostIds.Count} unique posts." : 
                    null
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch BlueSky feedback for {PostUrlOrId}", postUrlOrId);
            return null;
        }
    }

    /// <summary>
    /// Processes and tracks all replies in a thread view
    /// </summary>
    private void ProcessThreadReplies(BlueSkyThreadView threadView)
    {
        if (threadView.Replies == null || !threadView.Replies.Any())
        {
            return;
        }
        
        foreach (var reply in threadView.Replies)
        {
            if (reply.Post != null && !string.IsNullOrEmpty(reply.Post.Uri))
            {
                // Add to processed posts if not already there
                if (!_processedPostIds.Contains(reply.Post.Uri))
                {
                    _processedPostIds.Add(reply.Post.Uri);
                    
                    // Increment reply search counter
                    _currentReplySearches++;
                    
                    // Check for rate limit
                    if (_currentReplySearches >= _maxReplySearches)
                    {
                        _hitRateLimit = true;
                        _logger.LogWarning("Rate limit reached after processing {Count} replies", _currentReplySearches);
                        return;
                    }
                }
                
                // Process nested replies recursively
                ProcessThreadReplies(reply);
            }
        }
    }

    /// <summary>
    /// Safely extracts a property from a JsonElement with error handling
    /// </summary>
    private T GetJsonPropertySafe<T>(JsonElement element, string propertyName, T defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }
        
        try
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)(property.GetString() ?? string.Empty);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)property.GetInt32();
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)property.GetBoolean();
            }
            else if (typeof(T) == typeof(DateTime))
            {
                return (T)(object)property.GetDateTime();
            }
        }
        catch
        {
            return defaultValue;
        }
        
        return defaultValue;
    }
    
    public async Task<List<BlueSkyFeedbackItem>> SearchPostsAsync(string query, int maxResults = 25, DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        var results = new List<BlueSkyFeedbackItem>();

        // Authenticate first
        if (!await AuthenticateAsync(cancellationToken))
        {
            return results;
        }

        try
        {
            var escapedQuery = Uri.EscapeDataString(query);
            var searchUrl = $"https://public.api.bsky.app/xrpc/app.bsky.feed.searchPosts?q={escapedQuery}&limit={Math.Min(maxResults, 100)}&sort=latest";

            // Use authenticated request flow (handles token refresh / retry)
            var response = await GetAuthenticatedAsync(searchUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Handle rate limiting similarly to FetchFeedbackAsync
                if ((int)response.StatusCode == 429)
                {
                    _hitRateLimit = true;
                    _logger.LogWarning("Rate limit reached when performing BlueSky search for query: {Query}", query);
                    return results; // Return empty results when rate limited
                }

                _logger.LogWarning("BlueSky search failed with status {Status}", response.StatusCode);
                return results;
            }

            // Deserialize using source-generated System.Text.Json context
            var searchResponse = await response.Content.ReadFromJsonAsync(BlueSkyFeedbackJsonContext.Default.BlueSkySearchResponse, cancellationToken);

            if (searchResponse?.Posts == null)
            {
                _logger.LogWarning("BlueSky search returned null or empty data");
                return results;
            }

            foreach (var post in searchResponse.Posts)
            {
                try
                {
                    var authorHandle = post.Author?.Handle ?? "unknown";
                    var authorDisplayName = post.Author?.DisplayName ?? authorHandle;
                    var text = post.Record?.Text ?? "";
                    var createdAt = post.Record?.CreatedAt ?? post.IndexedAt;

                    var publishedAt = DateTimeOffset.TryParse(createdAt, out var dt) ? dt : DateTimeOffset.UtcNow;

                    // Apply date filter if specified
                    if (fromDate.HasValue && publishedAt < fromDate.Value)
                        continue;

                    results.Add(new BlueSkyFeedbackItem
                    {
                        Id = post.Cid,
                        Content = text,
                        Author = authorHandle,
                        AuthorName = authorDisplayName,
                        AuthorUsername = authorHandle,
                        TimestampUtc = publishedAt.UtcDateTime,
                        Replies = new List<BlueSkyFeedbackItem>()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing BlueSky post");
                    continue;
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
}
