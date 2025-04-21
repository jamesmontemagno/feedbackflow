using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SharedDump.Json;

namespace SharedDump.Models.Reddit;

public sealed class RedditService : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly int _maxRetries = 5;
    private bool _disposed;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry;

    public RedditService(string clientId, string clientSecret, HttpClient? client = null)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _client = client ?? new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FeedbackFlow", "1.0.0"));
    }

    private async Task EnsureAuthenticated()
    {
        if (!_client.DefaultRequestHeaders.Contains("Authorization"))
        {
            var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var token = await response.Content.ReadFromJsonAsync<RedditTokenResponse>();
            if (token?.AccessToken == null)
                throw new InvalidOperationException("Failed to get Reddit access token");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
    }

    private async Task EnsureValidTokenAsync()
    {
        if (_accessToken != null && _tokenExpiry > DateTimeOffset.UtcNow)
            return;

        var authString = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", authString) },
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            })
        };

        var response = await _client.SendAsync(tokenRequest);
        response.EnsureSuccessStatusCode();

        var tokenData = await response.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = tokenData.GetProperty("access_token").GetString();
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
    }

    public async Task<RedditThreadModel> GetThreadWithComments(string threadId)
    {
        ArgumentNullException.ThrowIfNull(threadId);

        for (var attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                await EnsureAuthenticated();
                
                var responseMessage = await _client.GetAsync($"https://oauth.reddit.com/comments/{threadId}?depth=100&limit=500");
                responseMessage.EnsureSuccessStatusCode();
                var content = await responseMessage.Content.ReadAsStringAsync();

                var response = System.Text.Json.JsonSerializer.Deserialize(
                    content, RedditJsonContext.Default.RedditListingArray);

                if (response == null || response.Length < 2)
                    throw new InvalidOperationException("Failed to get Reddit thread data - invalid response");

                var threadListing = response[0];
                if (threadListing?.Data?.Children == null || threadListing.Data.Children.Length == 0)
                    throw new InvalidOperationException("Failed to get Reddit thread data - missing thread data");

                var thread = threadListing.Data.Children[0].Data;
                var commentsList = new List<RedditCommentModel>();
                
                var commentListing = response[1];
                if (commentListing?.Data?.Children is { Length: > 0 } comments)
                {
                    ProcessComments(comments, commentsList);
                }

                return new RedditThreadModel
                {
                    Id = threadId,
                    Title = thread.Title ?? "[No Title]",
                    Author = thread.Author,
                    SelfText = thread.Selftext ?? string.Empty,
                    Url = $"https://www.reddit.com{thread.Permalink}",
                    Subreddit = "dotnet", // This is hardcoded since we only handle r/dotnet for now
                    Score = thread.Score,
                    UpvoteRatio = 1.0,
                    NumComments = commentsList.Count,
                    Comments = commentsList
                };
            }
            catch (HttpRequestException) when (attempt < _maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
                continue;
            }
        }

        throw new InvalidOperationException($"Failed to get Reddit thread data after {_maxRetries} attempts");
    }

    private static void ProcessComments(RedditThingData[] comments, List<RedditCommentModel> result)
    {
        foreach (var comment in comments)
        {
            if (string.IsNullOrEmpty(comment.Data?.Body)) continue;

            var model = new RedditCommentModel
            {
                Id = comment.Data.Id,
                ParentId = comment.Data.ParentId,
                Author = comment.Data.Author,
                Body = comment.Data.Body,
                Score = comment.Data.Score,
                Replies = new List<RedditCommentModel>()
            };

            result.Add(model);

            if (comment.Data.RepliesDisplay?.Data?.Children is { Length: > 0 } replies)
            {
                ProcessComments(replies, model.Replies);
            }
        }
    }

    public async Task<List<RedditThreadModel>> GetSubredditThreadsBasicInfo(string subreddit, string sortBy = "hot", DateTimeOffset? cutoffDate = null)
    {
        await EnsureValidTokenAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var url = $"https://oauth.reddit.com/r/{subreddit}/{sortBy}";
        var response = await _client.GetStringAsync(url);
        
        var listing = JsonSerializer.Deserialize(response, RedditJsonContext.Default.RedditListing);
        if (listing?.Data?.Children == null)
            return [];

        var threads = new List<RedditThreadModel>();
        foreach (var child in listing.Data.Children)
        {
            if (child.Data == null) continue;
            
            var created = DateTimeOffset.FromUnixTimeSeconds((long)child.Data.CreatedUtc);
            if (cutoffDate.HasValue && created < cutoffDate.Value)
                continue;

            threads.Add(new RedditThreadModel
            {
                Id = child.Data.Id,
                Title = child.Data.Title ?? "[No Title]",
                Author = child.Data.Author,
                SelfText = child.Data.Selftext ?? string.Empty,
                CreatedUtc = created,
                Score = child.Data.Score,
                NumComments = child.Data.Replies != null ? 1 : 0, // This will be populated when getting full thread
                Url = child.Data.Permalink.StartsWith("http") 
                    ? child.Data.Permalink 
                    : $"https://www.reddit.com{child.Data.Permalink}",
                Permalink = child.Data.Permalink,
                Subreddit = subreddit,
                UpvoteRatio = 1.0, // This isn't available in the listing response
                Comments = [] // Comments are only populated when getting full thread details
            });
        }

        return threads;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _client.Dispose();
        _disposed = true;
    }
}