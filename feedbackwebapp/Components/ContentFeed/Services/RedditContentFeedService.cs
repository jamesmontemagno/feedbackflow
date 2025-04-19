using System.Net.Http.Json;
using SharedDump.Models.Reddit;

namespace FeedbackWebApp.Components.ContentFeed.Services;

public class RedditContentFeedService : ContentFeedService, IRedditContentFeedService
{
    private readonly string _subreddit;
    private readonly int _days;
    private readonly string _sortBy;

    public RedditContentFeedService(
        string subreddit,
        int days,
        string sortBy,
        HttpClient http,
        IConfiguration configuration)
        : base(http, configuration)
    {
        _subreddit = subreddit;
        _days = days;
        _sortBy = sortBy;
    }

    public override async Task<object?> FetchContent()
    {
        return await ((IRedditContentFeedService)this).FetchContent();
    }

    async Task<List<RedditThreadModel>> IRedditContentFeedService.FetchContent()
    {
        return await Http.GetFromJsonAsync<List<RedditThreadModel>>(
            $"{BaseUrl}/api/GetTrendingRedditThreads?subreddit={Uri.EscapeDataString(_subreddit)}&days={_days}&sort={_sortBy}")
            ?? new List<RedditThreadModel>();
    }
}