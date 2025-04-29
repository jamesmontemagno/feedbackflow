using Microsoft.Extensions.Caching.Memory;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Services.ContentFeed;

public class HackerNewsCache
{
    private readonly IMemoryCache _cache;
    private const string CacheKey = "HackerNewsArticles";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public HackerNewsCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public List<HackerNewsItem>? GetCachedArticles()
    {
        _cache.TryGetValue(CacheKey, out List<HackerNewsItem>? articles);
        return articles;
    }

    public void CacheArticles(List<HackerNewsItem> articles)
    {
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration);
        _cache.Set(CacheKey, articles, cacheOptions);
    }
}