using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;

namespace FeedbackWebApp.Services.Feedback;

public class FeedbackServiceProvider
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly bool _useMocks;
    private readonly UserSettingsService _userSettings;

    public FeedbackServiceProvider(IConfiguration configuration, HttpClient http, UserSettingsService userSettings)
    {
        _configuration = configuration;
        _http = http;
        _userSettings = userSettings;
        _useMocks = configuration.GetValue<bool>("FeedbackApi:UseMocks");
    }

    public IYouTubeFeedbackService CreateYouTubeService(string videoIds, string playlistIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks 
            ? new MockYouTubeFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new YouTubeFeedbackService(_http, _configuration, _userSettings, videoIds, playlistIds, onStatusUpdate);
    }

    public IHackerNewsFeedbackService CreateHackerNewsService(string storyIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockHackerNewsFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new HackerNewsFeedbackService(_http, _configuration, _userSettings, storyIds, onStatusUpdate);
    }

    public IGitHubFeedbackService CreateGitHubService(string url, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockGitHubFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new GitHubFeedbackService(_http, _configuration, _userSettings, url, onStatusUpdate);
    }

    public IRedditFeedbackService CreateRedditService(string[] threadIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockRedditFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new RedditFeedbackService(threadIds, _http, _configuration, _userSettings, onStatusUpdate);
    }
}