using System.Net.Http.Headers;
using System.Net.Http.Json;
using SharedDump.Json;

namespace SharedDump.Models.Reddit;

public class RedditService
{
    private readonly HttpClient _client;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly int _maxRetries = 5;

    public RedditService(string clientId, string clientSecret, HttpClient? client = null)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
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
            
            var token = await response.Content.ReadFromJsonAsync<RedditTokenResponse>(RedditJsonContext.Default.RedditTokenResponse);
            if (token?.AccessToken == null)
                throw new InvalidOperationException("Failed to get Reddit access token");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
    }

    public async Task<RedditThreadModel> GetThreadWithComments(string threadId)
    {
        await EnsureAuthenticated();
        
        var responseMessage = await _client.GetAsync($"https://oauth.reddit.com/comments/{threadId}?depth=100&limit=500");
        responseMessage.EnsureSuccessStatusCode();
        var content = await responseMessage.Content.ReadAsStringAsync();

        
        var response = System.Text.Json.JsonSerializer.Deserialize(
            content, RedditJsonContext.Default.RedditListingArray);

        if (response?.Length < 2 || response?[0] == null)
            throw new InvalidOperationException("Failed to get Reddit thread data");

        var thread = response[0].Data.Children[0].Data;
        var commentsList = new List<RedditCommentModel>();
        ProcessComments(response[1].Data.Children, commentsList);

        return new RedditThreadModel
        {
            Id = threadId,
            Title = thread.Title ?? string.Empty,
            Author = thread.Author,
            SelfText = thread.Selftext ?? string.Empty,
            Url = $"https://www.reddit.com{thread.Permalink}",
            Subreddit = "dotnet", // This is hardcoded since we only handle r/dotnet for now
            //CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(thread.CreatedUtc).UtcDateTime,
            Score = thread.Score,
            UpvoteRatio = 1.0, // This field isn't available in comments endpoint
            NumComments = commentsList.Count,
            Comments = commentsList
        };
    }

    private void ProcessComments(RedditThingData[] comments, List<RedditCommentModel> result, string? parentId = null)
    {
        foreach (var comment in comments)
        {
            if (comment.Data.Body == null) continue; // Skip non-comment entries

            var model = new RedditCommentModel
            {
                Id = comment.Data.Id,
                ParentId = comment.Data.ParentId,
                Author = comment.Data.Author,
                Body = comment.Data.Body,
                //CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(comment.Data.CreatedUtc).UtcDateTime,
                Score = comment.Data.Score,
                Replies = null // Will be populated below if there are replies
            };

            result.Add(model);

            // Process replies if they exist
            if (comment.Data.RepliesDisplay?.Data?.Children is { Length: > 0 } replies)
            {
                var repliesList = new List<RedditCommentModel>();
                ProcessComments(replies, repliesList, comment.Data.Id);
                model.Replies = repliesList;
            }
        }
    }
}