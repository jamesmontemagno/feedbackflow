using System.Net.Http.Json;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Components.ContentFeed.Services;

public class HackerNewsContentFeedService : ContentFeedService, IHackerNewsContentFeedService
{
    private readonly string[]? _keywords;
    private readonly HackerNewsCache _cache;

    public HackerNewsContentFeedService(
        string[]? keywords,
        HttpClient http,
        IConfiguration configuration,
        HackerNewsCache cache)
        : base(http, configuration)
    {
        _keywords = keywords;
        _cache = cache;
    }

    public override async Task<object?> FetchContent()
    {
        return await ((IHackerNewsContentFeedService)this).FetchContent();
    }

    async Task<List<HackerNewsItem>> IHackerNewsContentFeedService.FetchContent()
    {
        var cachedArticles = _cache.GetCachedArticles();
        if (cachedArticles != null)
        {
            return _keywords != null && _keywords.Length > 0 
                ? cachedArticles.Where(article => 
                    _keywords.Any(keyword => 
                        article.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList()
                : cachedArticles;
        }

        var articles = await Http.GetFromJsonAsync<List<HackerNewsItem>>(
            _keywords != null && _keywords.Length > 0
                ? $"{BaseUrl}/api/SearchHackerNewsArticles?keywords={Uri.EscapeDataString(string.Join(",", _keywords))}"
                : $"{BaseUrl}/api/SearchHackerNewsArticles") 
            ?? new List<HackerNewsItem>();

        _cache.CacheArticles(articles);
        return articles;
    }
}