using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.Reddit;

namespace FeedbackWebApp.Services.ContentFeed;

public class RedditContentFeedService : ContentFeedService, IRedditContentFeedService
{
    private readonly string _subreddit;
    private readonly int _days;
    private readonly string _sortBy;

    public RedditContentFeedService(
        string subreddit,
        int days,
        string sortBy,
        IHttpClientFactory http,
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
        var redditCode = Configuration["FeedbackApi:FunctionsKey"] 
            ?? throw new InvalidOperationException("Reddit API code not configured");

        return await Http.GetFromJsonAsync<List<RedditThreadModel>>(
            $"{BaseUrl}/api/GetTrendingRedditThreads?code={Uri.EscapeDataString(redditCode)}&subreddit={Uri.EscapeDataString(_subreddit)}&days={_days}&sort={_sortBy}")
            ?? new List<RedditThreadModel>();
    }
}