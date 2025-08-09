using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Mock;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Utils;

namespace FeedbackWebApp.Services.Feedback;

public class FeedbackServiceProvider
{    
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _http;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly bool _useMocks;
    private readonly UserSettingsService _userSettings;

    public FeedbackServiceProvider(
        IConfiguration configuration, 
        IHttpClientFactory http, 
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService)
    {
        _configuration = configuration;
        _http = http;
        _userSettings = userSettings;
        _authHeaderService = authHeaderService;
        _useMocks = configuration.GetValue<bool>("FeedbackApi:UseMocks");
    }

    public IYouTubeFeedbackService CreateYouTubeService(string videoIds, string playlistIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks 
            ? new MockYouTubeFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new YouTubeFeedbackService(_http, _configuration, _userSettings, _authHeaderService, videoIds, playlistIds, onStatusUpdate);
    }

    public IHackerNewsFeedbackService CreateHackerNewsService(string storyIds, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockHackerNewsFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new HackerNewsFeedbackService(_http, _configuration, _userSettings, _authHeaderService, storyIds, onStatusUpdate);
    }

    public IGitHubFeedbackService CreateGitHubService(string url, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockGitHubFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new GitHubFeedbackService(_http, _configuration, _userSettings, _authHeaderService, url, onStatusUpdate);
    }

    public IRedditFeedbackService CreateRedditService(string threadId, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockRedditFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new RedditFeedbackService(threadId, _http, _configuration, _userSettings, _authHeaderService, onStatusUpdate);
    }

    public IDevBlogsFeedbackService CreateDevBlogsService(string articleUrl, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockDevBlogsFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate) { ArticleUrl = articleUrl }
            : new DevBlogsFeedbackService(_http, _configuration, _userSettings, _authHeaderService, articleUrl, onStatusUpdate);
    }

    public ITwitterFeedbackService CreateTwitterService(string tweetUrlOrId, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockTwitterFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new TwitterFeedbackService(_http, _configuration, _userSettings, _authHeaderService, tweetUrlOrId, onStatusUpdate);
    }

    public IBlueSkyFeedbackService CreateBlueSkyService(string postUrlOrId, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockBlueSkyFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate)
            : new BlueSkyFeedbackService(_http, _configuration, _userSettings, _authHeaderService, postUrlOrId, onStatusUpdate);
    }

    public IManualFeedbackService CreateManualService(string content, string? customPrompt = null, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        return _useMocks
            ? new MockManualFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate) 
              { 
                  Content = content,
                  CustomPrompt = customPrompt ?? string.Empty
              }
            : new ManualFeedbackService(_http, _configuration, _userSettings, _authHeaderService, content, customPrompt, onStatusUpdate);
    }

    public IFeedbackService CreateAutoDataSourceService(string[] urls, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        if (_useMocks)
        {
            // If using mocks and only one URL is provided, return the specific mock service for that platform
            if (urls.Length == 1)
            {
                var singleUrl = urls[0];
                var specificService = GetService(singleUrl);
                
                // If we can identify a specific service type, return it cast as IAutoDataSourceFeedbackService
                // This provides more realistic mock behavior for single-platform scenarios
                if (specificService is IFeedbackService feedbackService)
                {
                    return feedbackService;
                }
            }
            
            // For multiple URLs or unrecognized single URLs, use the general auto data source mock
            return new MockAutoDataSourceFeedbackService(_http, _configuration, _userSettings, _authHeaderService, onStatusUpdate);
        }
        
        return new AutoDataSourceFeedbackService(_http, _configuration, _userSettings, _authHeaderService, this, urls, onStatusUpdate);
    }
    public IFeedbackService? GetService(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        // YouTube URLs
        if (UrlParsing.IsYouTubeUrl(url))
        {
            var videoId = UrlParsing.ExtractVideoId(url) ?? string.Empty;
            return CreateYouTubeService(videoId, string.Empty);
        }        // GitHub URLs
        if (UrlParsing.IsGitHubUrl(url))
            return CreateGitHubService(url);        
            
        // Reddit URLs        
        if (UrlParsing.IsRedditUrl(url))
            return CreateRedditService(url);

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

    public async Task<HttpResponseMessage?> GetTierLimitsAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] ?? "http://localhost:7071";
            var code = _configuration["FeedbackApi:FunctionsKey"] ?? "";
            
            var client = _http.CreateClient();
            var url = $"{baseUrl}/api/GetTierLimits?code={code}";
            
            var response = await client.GetAsync(url);
            return response;
        }
        catch (Exception)
        {
            return null;
        }
    }
}