using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;
using SharedDump.Utils;

namespace FeedbackWebApp.Services.Feedback;

public class FeedbackServiceProvider
{    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly bool _useMocks;
    private readonly UserSettingsService _userSettings;

    public FeedbackServiceProvider(IConfiguration configuration, IHttpClientFactory http, UserSettingsService userSettings)
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

    public IDevBlogsFeedbackService CreateDevBlogsService(string articleUrl, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockDevBlogsFeedbackService(_http, _configuration, _userSettings, onStatusUpdate) { ArticleUrl = articleUrl }
            : new DevBlogsFeedbackService(_http, _configuration, _userSettings, articleUrl, onStatusUpdate);
    }

    public ITwitterFeedbackService CreateTwitterService(string tweetUrlOrId, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockTwitterFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new TwitterFeedbackService(_http, _configuration, _userSettings, tweetUrlOrId, onStatusUpdate);
    }

    public IBlueSkyFeedbackService CreateBlueSkyService(string postUrlOrId, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockBlueSkyFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new BlueSkyFeedbackService(_http, _configuration, _userSettings, postUrlOrId, onStatusUpdate);
    }

    public IManualFeedbackService CreateManualService(string content, string? customPrompt = null, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockManualFeedbackService(_http, _configuration, _userSettings, onStatusUpdate) 
              { 
                  Content = content,
                  CustomPrompt = customPrompt ?? string.Empty
              }
            : new ManualFeedbackService(_http, _configuration, _userSettings, content, customPrompt, onStatusUpdate);
    }

    public IAutoDataSourceFeedbackService CreateAutoDataSourceService(string[] urls, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockAutoDataSourceFeedbackService(_http, _configuration, _userSettings, onStatusUpdate)
            : new AutoDataSourceFeedbackService(_http, _configuration, _userSettings, this, urls, onStatusUpdate);
    }
    public IFeedbackService? GetService(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        // YouTube URLs
        if (UrlParsing.IsYouTubeUrl(url))
        {
            var videoId = UrlParsing.ExtractYouTubeId(url) ?? string.Empty;
            return CreateYouTubeService(videoId, string.Empty);
        }

        // GitHub URLs
        if (UrlParsing.IsGitHubUrl(url))
            return CreateGitHubService(url);        // Reddit URLs
        if (UrlParsing.IsRedditUrl(url))
            return CreateRedditService(new[] { url });

        // Hacker News URLs
        if (UrlParsing.IsHackerNewsUrl(url))
        {
            var storyId = UrlParsing.ExtractHackerNewsId(url) ?? string.Empty;
            return CreateHackerNewsService(storyId);
        }

        // Twitter URLs
        if (UrlParsing.IsTwitterUrl(url))
            return CreateTwitterService(url);

        // BlueSky URLs
        if (UrlParsing.IsBlueSkyUrl(url))
            return CreateBlueSkyService(url);

        // Dev.to or Microsoft DevBlogs URLs
        if (UrlParsing.IsDevBlogsUrl(url))
            return CreateDevBlogsService(url);

        // If no specific service matches, treat it as a manual input
        return CreateManualService(url);
    }
}