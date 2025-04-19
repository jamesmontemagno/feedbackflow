using System.Net.Http.Json;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Components.ContentFeed.Services;

public class HackerNewsContentFeedService : ContentFeedService, IHackerNewsContentFeedService
{
    private readonly string[] _keywords;

    public HackerNewsContentFeedService(
        string[] keywords,
        HttpClient http,
        IConfiguration configuration)
        : base(http, configuration)
    {
        _keywords = keywords;
    }

    public override async Task<object?> FetchContent()
    {
        return await ((IHackerNewsContentFeedService)this).FetchContent();
    }

    async Task<List<HackerNewsItem>> IHackerNewsContentFeedService.FetchContent()
    {
        return await Http.GetFromJsonAsync<List<HackerNewsItem>>(
            $"{BaseUrl}/api/SearchHackerNewsArticles?keywords={Uri.EscapeDataString(string.Join(",", _keywords))}")
            ?? new List<HackerNewsItem>();
    }
}