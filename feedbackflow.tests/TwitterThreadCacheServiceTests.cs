using FeedbackFunctions.Services.Twitter;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedDump.Json;
using SharedDump.Models.TwitterFeedback;
using System.Threading;

namespace FeedbackFlow.Tests;

[TestClass]
public class TwitterThreadCacheServiceTests
{
    private ILogger<TwitterThreadCacheService> _logger = null!;
    private IConfiguration _configuration = null!;
    private IDistributedCache _distributedCache = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<TwitterThreadCacheService>>();
        _configuration = Substitute.For<IConfiguration>();
        _distributedCache = Substitute.For<IDistributedCache>();
        _configuration["Twitter:ThreadCacheTTL"].Returns("00:05:00");
        _configuration["Twitter:UseL2Cache"].Returns("false");
    }

    [TestMethod]
    public async Task GetThreadAsync_RepeatedKey_ReturnsCacheHitWithoutRefetch()
    {
        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);
        var fetchCount = 0;
        var response = CreateResponse("123");

        var first = await service.GetThreadAsync(
            "123",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(response);
            });

        var second = await service.GetThreadAsync(
            "123",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("123"));
            });

        Assert.IsFalse(first.CacheHit);
        Assert.IsTrue(second.CacheHit);
        Assert.AreEqual(1, fetchCount);
        Assert.IsNotNull(second.Response);
        Assert.AreEqual("123", second.Response.Items[0].Id);
    }

    [TestMethod]
    public async Task GetThreadAsync_ForceRefresh_BypassesCache()
    {
        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);
        var fetchCount = 0;

        await service.GetThreadAsync(
            "123",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("123"));
            });

        var refreshed = await service.GetThreadAsync(
            "123",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("123"));
            },
            forceRefresh: true);

        Assert.IsFalse(refreshed.CacheHit);
        Assert.AreEqual(2, fetchCount);
    }

    [TestMethod]
    public async Task GetThreadAsync_ConcurrentCalls_SingleFlightsFetch()
    {
        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);
        var fetchCount = 0;

        async Task<TwitterFeedbackResponse?> FetchAsync()
        {
            Interlocked.Increment(ref fetchCount);
            await Task.Delay(150);
            return CreateResponse("999");
        }

        var firstTask = service.GetThreadAsync("999", FetchAsync);
        var secondTask = service.GetThreadAsync("999", FetchAsync);

        await Task.WhenAll(firstTask, secondTask);

        Assert.AreEqual(1, fetchCount);
        var cacheHitCount = new[] { firstTask.Result, secondTask.Result }.Count(result => result.CacheHit);
        var cacheMissCount = new[] { firstTask.Result, secondTask.Result }.Count(result => !result.CacheHit);
        Assert.AreEqual(1, cacheHitCount);
        Assert.AreEqual(1, cacheMissCount);
    }

    [TestMethod]
    public async Task GetThreadAsync_L2CacheHit_ReturnsWithoutRefetch()
    {
        // Arrange
        _configuration["Twitter:UseL2Cache"].Returns("true");
        _configuration["Twitter:ThreadL2CacheTTL"].Returns("00:30:00");
        var l2Response = CreateResponse("42");
        var l2Payload = System.Text.Json.JsonSerializer.Serialize(
            l2Response,
            TwitterFeedbackJsonContext.Default.TwitterFeedbackResponse);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(System.Text.Encoding.UTF8.GetBytes(l2Payload));

        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);
        var fetchCount = 0;

        // Act
        var result = await service.GetThreadAsync(
            "https://x.com/user/status/42",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("42"));
            });

        // Assert
        Assert.IsTrue(result.CacheHit);
        Assert.AreEqual(0, fetchCount);
        Assert.IsNotNull(result.Response);
        Assert.AreEqual("42", result.Response.Items[0].Id);
    }

    [TestMethod]
    public async Task GetThreadAsync_InvalidL2Payload_FallsBackToFetch()
    {
        _configuration["Twitter:UseL2Cache"].Returns("true");
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(System.Text.Encoding.UTF8.GetBytes("{invalid-json"));

        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);
        var fetchCount = 0;

        var result = await service.GetThreadAsync(
            "https://x.com/user/status/55",
            () =>
            {
                Interlocked.Increment(ref fetchCount);
                return Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("55"));
            });

        Assert.AreEqual(1, fetchCount);
        Assert.IsFalse(result.CacheHit);
        Assert.IsNotNull(result.Response);
        await _distributedCache.Received(2).RemoveAsync("raw:https://x.com/user/status/55", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task GetThreadAsync_L2WriteFailure_DoesNotFailRequest()
    {
        _configuration["Twitter:UseL2Cache"].Returns("true");
        _distributedCache
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("cache unavailable")));

        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);

        var result = await service.GetThreadAsync(
            "https://x.com/user/status/77",
            () => Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("77")));

        Assert.IsNotNull(result.Response);
        Assert.AreEqual("77", result.Response.Items[0].Id);
        Assert.IsFalse(result.CacheHit);
    }

    [TestMethod]
    public async Task GetThreadAsync_L2ReadCancellation_PropagatesCancellation()
    {
        _configuration["Twitter:UseL2Cache"].Returns("true");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromCanceled<byte[]?>(cts.Token));

        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => service.GetThreadAsync(
            "https://x.com/user/status/88",
            () => Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("88")),
            cancellationToken: cts.Token));
    }

    [TestMethod]
    public async Task GetThreadAsync_L2WriteCancellation_PropagatesCancellation()
    {
        _configuration["Twitter:UseL2Cache"].Returns("true");
        using var cts = new CancellationTokenSource();
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));
        _distributedCache
            .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            });

        var service = new TwitterThreadCacheService(_logger, _configuration, _distributedCache);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => service.GetThreadAsync(
            "https://x.com/user/status/99",
            () => Task.FromResult<TwitterFeedbackResponse?>(CreateResponse("99")),
            cancellationToken: cts.Token));
    }

    private static TwitterFeedbackResponse CreateResponse(string id) =>
        new()
        {
            Items =
            [
                new TwitterFeedbackItem
                {
                    Id = id,
                    Author = "tester",
                    AuthorName = "Tester",
                    AuthorUsername = "tester",
                    Content = "hello",
                    TimestampUtc = DateTime.UtcNow,
                    Replies = []
                }
            ],
            ProcessedTweetCount = 1
        };
}
