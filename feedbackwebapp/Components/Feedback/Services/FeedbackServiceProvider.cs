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

    public IYouTubeFeedbackService CreateYouTubeService(string videoIds, string playlistIds)
    {
        return _useMocks 
            ? new MockYouTubeFeedbackService()
            : new YouTubeFeedbackService(_http, _configuration, videoIds, playlistIds);
    }

    public IHackerNewsFeedbackService CreateHackerNewsService(string storyIds)
    {
        return _useMocks
            ? new MockHackerNewsFeedbackService()
            : new HackerNewsFeedbackService(_http, _configuration, storyIds);
    }
}