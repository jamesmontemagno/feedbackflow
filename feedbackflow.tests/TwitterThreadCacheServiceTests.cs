using FeedbackFunctions.Services.Twitter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedDump.Models.TwitterFeedback;
using System.Threading;

namespace FeedbackFlow.Tests;

[TestClass]
public class TwitterThreadCacheServiceTests
{
    private ILogger<TwitterThreadCacheService> _logger = null!;
    private IConfiguration _configuration = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<TwitterThreadCacheService>>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration["Twitter:ThreadCacheTTL"].Returns("00:05:00");
    }

    [TestMethod]
    public async Task GetThreadAsync_RepeatedKey_ReturnsCacheHitWithoutRefetch()
    {
        var service = new TwitterThreadCacheService(_logger, _configuration);
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
        var service = new TwitterThreadCacheService(_logger, _configuration);
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
        var service = new TwitterThreadCacheService(_logger, _configuration);
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
        Assert.IsFalse(firstTask.Result.CacheHit);
        Assert.IsTrue(secondTask.Result.CacheHit);
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
