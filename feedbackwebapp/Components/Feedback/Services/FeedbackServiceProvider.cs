namespace FeedbackWebApp.Components.Feedback.Services;

public class FeedbackServiceProvider
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly bool _useMocks;

    public FeedbackServiceProvider(IConfiguration configuration, HttpClient http)
    {
        _configuration = configuration;
        _http = http;
        _useMocks = configuration.GetValue<bool>("FeedbackApi:UseMocks");
    }

    public IYouTubeFeedbackService CreateYouTubeService(string videoIds, string playlistIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks 
            ? new MockYouTubeFeedbackService(_http, _configuration, onStatusUpdate)
            : new YouTubeFeedbackService(_http, _configuration, videoIds, playlistIds, onStatusUpdate);
    }

    public IHackerNewsFeedbackService CreateHackerNewsService(string storyIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockHackerNewsFeedbackService(_http, _configuration, onStatusUpdate)
            : new HackerNewsFeedbackService(_http, _configuration, storyIds, onStatusUpdate);
    }
    
    public IGitHubFeedbackService CreateGitHubService(string url, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockGitHubFeedbackService(_http, _configuration, onStatusUpdate)
            : new GitHubSingleItemFeedbackService(_http, _configuration, url, onStatusUpdate);
    }
}