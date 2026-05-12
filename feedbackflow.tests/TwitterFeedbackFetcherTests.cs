using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SharedDump.Models.TwitterFeedback;

namespace FeedbackFlow.Tests;

[TestClass]
public class TwitterFeedbackFetcherTests
{
    private QueuedHttpMessageHandler _handler = null!;
    private HttpClient _httpClient = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new QueuedHttpMessageHandler();
        _httpClient = new HttpClient(_handler);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    [TestMethod]
    [TestCategory("TwitterFeedbackFetcher")]
    public async Task FetchFeedbackAsync_WhenRepliesExceedCap_TruncatesAndSetsMayBeIncomplete()
    {
        // Arrange: cap = 3, API returns 5 replies
        const int maxReplies = 3;
        var fetcher = new TwitterFeedbackFetcher(
            _httpClient,
            NullLogger<TwitterFeedbackFetcher>.Instance,
            maxReplies);

        const string tweetId = "1234567890100"; // valid 13-digit snowflake
        _handler.Enqueue(BuildMainTweetResponse(tweetId, tweetId));
        _handler.Enqueue(BuildRepliesResponse(tweetId, replyCount: 5, hasNextPage: false));

        // Act
        var result = await fetcher.FetchFeedbackAsync(tweetId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.MayBeIncomplete, "MayBeIncomplete should be true when reply cap is hit.");
        Assert.IsNotNull(result.RateLimitInfo, "RateLimitInfo should describe the cap.");
        StringAssert.Contains(result.RateLimitInfo, "capped", StringComparison.OrdinalIgnoreCase);

        // Count all replies in the tree (they are flat before tree organization, but after tree build
        // they are nested — count total across all nested levels)
        var totalReplies = CountAllReplies(result.Items);
        Assert.IsLessThanOrEqualTo(totalReplies, maxReplies,
            $"Expected at most {maxReplies} replies but got {totalReplies}.");
    }

    [TestMethod]
    [TestCategory("TwitterFeedbackFetcher")]
    public async Task FetchFeedbackAsync_WhenRepliesUnderCap_DoesNotSetMayBeIncomplete()
    {
        // Arrange: cap = 10, API returns 3 replies
        var fetcher = new TwitterFeedbackFetcher(
            _httpClient,
            NullLogger<TwitterFeedbackFetcher>.Instance,
            maxReplies: 10);

        const string tweetId = "1234567890200"; // valid 13-digit snowflake
        _handler.Enqueue(BuildMainTweetResponse(tweetId, tweetId));
        _handler.Enqueue(BuildRepliesResponse(tweetId, replyCount: 3, hasNextPage: false));

        // Act
        var result = await fetcher.FetchFeedbackAsync(tweetId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.MayBeIncomplete, "MayBeIncomplete should be false when under cap.");
        Assert.IsNull(result.RateLimitInfo, "RateLimitInfo should be null when not truncated.");

        var totalReplies = CountAllReplies(result.Items);
        Assert.AreEqual(3, totalReplies);
    }

    [TestMethod]
    [TestCategory("TwitterFeedbackFetcher")]
    public async Task FetchFeedbackAsync_WhenCapIsExactlyReplyCount_SetsMayBeIncomplete()
    {
        // When exactly at cap we can't distinguish "got all" from "stopped early",
        // so MayBeIncomplete is conservatively set to true.
        var fetcher = new TwitterFeedbackFetcher(
            _httpClient,
            NullLogger<TwitterFeedbackFetcher>.Instance,
            maxReplies: 3);

        const string tweetId = "1234567890300"; // valid 13-digit snowflake
        _handler.Enqueue(BuildMainTweetResponse(tweetId, tweetId));
        _handler.Enqueue(BuildRepliesResponse(tweetId, replyCount: 3, hasNextPage: false));

        // Act
        var result = await fetcher.FetchFeedbackAsync(tweetId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.MayBeIncomplete,
            "MayBeIncomplete should be true when reply count equals cap (conservative truncation signal).");

        var totalReplies = CountAllReplies(result.Items);
        Assert.AreEqual(3, totalReplies);
    }

    // --- Helpers ---

    private static int CountAllReplies(IEnumerable<TwitterFeedbackItem> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Replies is { Count: > 0 })
            {
                count += item.Replies.Count;
                count += CountAllReplies(item.Replies);
            }
        }
        return count;
    }

    private static HttpResponseMessage BuildMainTweetResponse(string tweetId, string conversationId)
    {
        var json = $$"""
        {
          "data": {
            "id": "{{tweetId}}",
            "author_id": "user1",
            "conversation_id": "{{conversationId}}",
            "created_at": "2024-01-01T00:00:00Z",
            "text": "Root tweet {{tweetId}}"
          },
          "includes": {
            "users": [
              {"id": "user1", "name": "Test User", "username": "testuser"}
            ]
          }
        }
        """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage BuildRepliesResponse(
        string conversationId, int replyCount, bool hasNextPage)
    {
        var replies = new StringBuilder();
        for (var i = 1; i <= replyCount; i++)
        {
            if (i > 1) replies.Append(',');
            replies.Append($$"""
            {
              "id": "{{conversationId}}{{i:D3}}",
              "author_id": "user{{i + 1}}",
              "created_at": "2024-01-01T00:0{{i}}:00Z",
              "text": "Reply {{i}}",
              "referenced_tweets": [{"type": "replied_to", "id": "{{conversationId}}"}]
            }
            """);
        }

        var users = new StringBuilder();
        for (var i = 1; i <= replyCount; i++)
        {
            if (i > 1) users.Append(',');
            users.Append($$"""{"id": "user{{i + 1}}", "name": "User {{i + 1}}", "username": "user{{i + 1}}"}""");
        }

        var nextToken = hasNextPage ? ""","next_token": "nextpage123" """ : string.Empty;
        var json = $$"""
        {
          "data": [{{replies}}],
          "includes": {"users": [{{users}}]},
          "meta": {"result_count": {{replyCount}}{{nextToken}}}
        }
        """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}

/// <summary>
/// HTTP message handler that returns pre-queued responses in order.
/// </summary>
internal class QueuedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _queue = new();

    public void Enqueue(HttpResponseMessage response) => _queue.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_queue.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        return Task.FromResult(_queue.Dequeue());
    }
}
