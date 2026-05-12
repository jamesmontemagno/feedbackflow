using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Utils;

namespace FeedbackFunctions.Services.Twitter;

public class TwitterThreadCacheService : ITwitterThreadCacheService
{
    private readonly ILogger<TwitterThreadCacheService> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly ConcurrentDictionary<string, CachedThread> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks = new(StringComparer.OrdinalIgnoreCase);

    public TwitterThreadCacheService(
        ILogger<TwitterThreadCacheService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _cacheTtl = TimeSpan.Parse(configuration["Twitter:ThreadCacheTTL"] ?? "00:05:00");
    }

    public async Task<TwitterThreadCacheResult> GetThreadAsync(
        string tweetUrlOrId,
        Func<Task<TwitterFeedbackResponse?>> fetchThreadAsync,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(tweetUrlOrId);
        if (!forceRefresh && TryGetValidEntry(cacheKey, out var cachedResponse))
        {
            return new TwitterThreadCacheResult(cachedResponse, true, cacheKey);
        }

        var cacheLock = _cacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetValidEntry(cacheKey, out cachedResponse))
            {
                return new TwitterThreadCacheResult(cachedResponse, true, cacheKey);
            }

            var response = await fetchThreadAsync();
            if (response is not null)
            {
                _cache[cacheKey] = new CachedThread(response, DateTimeOffset.UtcNow);
            }

            _logger.LogInformation(
                "TwitterThreadCache fetched upstream result for key {CacheKey}. forceRefresh={ForceRefresh}, cached={Cached}, ttlSeconds={TtlSeconds}",
                cacheKey,
                forceRefresh,
                response is not null,
                _cacheTtl.TotalSeconds);

            return new TwitterThreadCacheResult(response, false, cacheKey);
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private bool TryGetValidEntry(string cacheKey, out TwitterFeedbackResponse? response)
    {
        if (_cache.TryGetValue(cacheKey, out var cached) &&
            DateTimeOffset.UtcNow - cached.CachedAt < _cacheTtl)
        {
            response = cached.Response;
            return true;
        }

        _cache.TryRemove(cacheKey, out _);
        response = null;
        return false;
    }

    private static string BuildCacheKey(string tweetUrlOrId)
    {
        var tweetId = TwitterUrlParser.ExtractTweetId(tweetUrlOrId);
        if (!string.IsNullOrWhiteSpace(tweetId))
        {
            return $"tweet:{tweetId.Trim().ToLowerInvariant()}";
        }

        return $"raw:{tweetUrlOrId.Trim().ToLowerInvariant()}";
    }

    private readonly record struct CachedThread(TwitterFeedbackResponse Response, DateTimeOffset CachedAt);
}
