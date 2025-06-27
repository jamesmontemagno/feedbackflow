using SharedDump.Models.HackerNews;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.ContentFeed;

public class HackerNewsContentFeedService : ContentFeedService, IHackerNewsContentFeedService
{
    private readonly string[]? _keywords;
    private readonly HackerNewsCache _cache;

    public HackerNewsContentFeedService(
        string[]? keywords,
        IHttpClientFactory http,
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

        var hackerNewsCode = Configuration["FeedbackApi:FunctionsKey"] 
            ?? throw new InvalidOperationException("HackerNews API code not configured");

        var articles = await Http.GetFromJsonAsync<List<HackerNewsItem>>(
            _keywords != null && _keywords.Length > 0
                ? $"{BaseUrl}/api/SearchHackerNewsArticles?code={Uri.EscapeDataString(hackerNewsCode)}&keywords={Uri.EscapeDataString(string.Join(",", _keywords))}"
                : $"{BaseUrl}/api/SearchHackerNewsArticles?code={Uri.EscapeDataString(hackerNewsCode)}") 
            ?? new List<HackerNewsItem>();

        _cache.CacheArticles(articles);
        return articles;
    }
}