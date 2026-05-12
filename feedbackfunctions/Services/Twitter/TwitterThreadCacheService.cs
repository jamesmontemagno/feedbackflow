using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Json;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Utils;

namespace FeedbackFunctions.Services.Twitter;

public class TwitterThreadCacheService : ITwitterThreadCacheService
{
    private readonly ILogger<TwitterThreadCacheService> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _l2CacheTtl;
    private readonly bool _useL2Cache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ConcurrentDictionary<string, CachedThread> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks = new(StringComparer.OrdinalIgnoreCase);

    public TwitterThreadCacheService(
        ILogger<TwitterThreadCacheService> logger,
        IConfiguration configuration,
        IDistributedCache? distributedCache = null)
    {
        _logger = logger;
        _distributedCache = distributedCache;
        var cacheTtlConfig = configuration["Twitter:ThreadCacheTTL"];
        if (!TimeSpan.TryParse(cacheTtlConfig, out _cacheTtl))
        {
            _cacheTtl = TimeSpan.FromMinutes(5);
            _logger.LogWarning(
                "Invalid Twitter:ThreadCacheTTL value '{ConfiguredValue}'. Using default {DefaultTtl}.",
                cacheTtlConfig,
                _cacheTtl);
        }

        _useL2Cache = bool.TryParse(configuration["Twitter:UseL2Cache"], out var useL2Cache) && useL2Cache;

        var l2CacheTtlConfig = configuration["Twitter:ThreadL2CacheTTL"];
        if (!TimeSpan.TryParse(l2CacheTtlConfig, out _l2CacheTtl))
        {
            _l2CacheTtl = TimeSpan.FromMinutes(30);
        }

        if (_useL2Cache && _distributedCache is null)
        {
            _logger.LogWarning(
                "Twitter:UseL2Cache is enabled but no IDistributedCache is registered. Running with L1 cache only.");
        }
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

        if (!forceRefresh)
        {
            var l2CachedResponse = await TryGetL2EntryAsync(cacheKey, cancellationToken);
            if (l2CachedResponse is not null)
            {
                _cache[cacheKey] = new CachedThread(l2CachedResponse, DateTimeOffset.UtcNow);
                return new TwitterThreadCacheResult(l2CachedResponse, true, cacheKey);
            }
        }

        var cacheLock = _cacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetValidEntry(cacheKey, out cachedResponse))
            {
                return new TwitterThreadCacheResult(cachedResponse, true, cacheKey);
            }

            if (!forceRefresh)
            {
                var l2CachedResponse = await TryGetL2EntryAsync(cacheKey, cancellationToken);
                if (l2CachedResponse is not null)
                {
                    _cache[cacheKey] = new CachedThread(l2CachedResponse, DateTimeOffset.UtcNow);
                    return new TwitterThreadCacheResult(l2CachedResponse, true, cacheKey);
                }
            }

            var response = await fetchThreadAsync();
            if (response is not null)
            {
                _cache[cacheKey] = new CachedThread(response, DateTimeOffset.UtcNow);
                await SetL2EntryAsync(cacheKey, response, cancellationToken);
            }

            _logger.LogInformation(
                "TwitterThreadCache fetched upstream result for key {CacheKey}. forceRefresh={ForceRefresh}, storedInCache={StoredInCache}, l1TtlSeconds={L1TtlSeconds}, l2Enabled={L2Enabled}",
                cacheKey,
                forceRefresh,
                response is not null,
                _cacheTtl.TotalSeconds,
                _useL2Cache);

            return new TwitterThreadCacheResult(response, false, cacheKey);
        }
        finally
        {
            cacheLock.Release();
            _cacheLocks.TryRemove(cacheKey, out _);
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

    private async Task<TwitterFeedbackResponse?> TryGetL2EntryAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (!_useL2Cache || _distributedCache is null)
        {
            return null;
        }

        var cachedPayload = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(cachedPayload))
        {
            return null;
        }

        return JsonSerializer.Deserialize(cachedPayload, TwitterFeedbackJsonContext.Default.TwitterFeedbackResponse);
    }

    private async Task SetL2EntryAsync(string cacheKey, TwitterFeedbackResponse response, CancellationToken cancellationToken)
    {
        if (!_useL2Cache || _distributedCache is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(response, TwitterFeedbackJsonContext.Default.TwitterFeedbackResponse);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _l2CacheTtl
        };
        await _distributedCache.SetStringAsync(cacheKey, payload, options, cancellationToken);
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
