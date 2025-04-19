namespace FeedbackWebApp.Components.ContentFeed.Services;

public class ContentFeedServiceProvider
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly bool _useMocks;

    public ContentFeedServiceProvider(IConfiguration configuration, HttpClient http)
    {
        _configuration = configuration;
        _http = http;
        _useMocks = configuration.GetValue<bool>("FeedbackApi:UseMocks");
    }

    public IYouTubeContentFeedService CreateYouTubeService(string topic, int days, string? tag = null)
    {
        return _useMocks
            ? new MockYouTubeContentFeedService(_http, _configuration)
            : new YouTubeContentFeedService(topic, days, tag, _http, _configuration);
    }

    public IRedditContentFeedService CreateRedditService(string subreddit, int days, string sortBy)
    {
        return _useMocks
            ? new MockRedditContentFeedService(_http, _configuration)
            : new RedditContentFeedService(subreddit, days, sortBy, _http, _configuration);
    }

    public IHackerNewsContentFeedService CreateHackerNewsService(string[] keywords)
    {
        return _useMocks
            ? new MockHackerNewsContentFeedService(_http, _configuration)
            : new HackerNewsContentFeedService(keywords, _http, _configuration);
    }
}