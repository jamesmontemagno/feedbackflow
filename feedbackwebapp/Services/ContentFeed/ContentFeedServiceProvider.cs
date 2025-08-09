using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;
using Microsoft.Extensions.Caching.Memory;

namespace FeedbackWebApp.Services.ContentFeed;

public class ContentFeedServiceProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly bool _useMocks;
    private readonly IMemoryCache _cache;
    private readonly HackerNewsCache _hackerNewsCache;

    public ContentFeedServiceProvider(IConfiguration configuration, IHttpClientFactory http, IMemoryCache cache)
    {
        _configuration = configuration;
        _http = http;
        _useMocks = configuration.GetValue<bool>("FeedbackApi:UseMocks");
        _cache = cache;
        _hackerNewsCache = new HackerNewsCache(cache);
    }

    public IYouTubeContentFeedService CreateYouTubeService(string topic, int days, string? tag = null)
    {
        return _useMocks
            ? new MockYouTubeContentFeedService(_http, _configuration)
            : new YouTubeContentFeedService(topic, days, tag, _http, _configuration);
    }

    public IRedditContentFeedService CreateRedditService(string subreddit, int days, string sortBy, Authentication.IAuthenticationHeaderService? authHeaderService = null)
    {
        return _useMocks
            ? new MockRedditContentFeedService(_http, _configuration)
            : new RedditContentFeedService(subreddit, days, sortBy, _http, _configuration, authHeaderService ?? throw new InvalidOperationException("Auth header service required"));
    }

    public IHackerNewsContentFeedService CreateHackerNewsService(string[]? keywords = null, Authentication.IAuthenticationHeaderService? authHeaderService = null)
    {
        return _useMocks
            ? new MockHackerNewsContentFeedService(_http, _configuration)
            : new HackerNewsContentFeedService(keywords, _http, _configuration, _hackerNewsCache, authHeaderService ?? throw new InvalidOperationException("Auth header service required"));
    }
}