using FeedbackWebApp.Services.Interfaces;
using SharedDump.Utils;

namespace FeedbackWebApp.Services.Feedback;

public class AutoDataSourceFeedbackService: FeedbackService, IAutoDataSourceFeedbackService
{
    private readonly string[] _urls;
    private readonly FeedbackServiceProvider _serviceProvider;

    public AutoDataSourceFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackServiceProvider serviceProvider,
        string[] urls,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _urls = urls;
        _serviceProvider = serviceProvider;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        if (_urls == null || _urls.Length == 0)
        {
            throw new InvalidOperationException("Please provide at least one URL to analyze");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Analyzing source URLs...");

        var allComments = new List<string>();
        var processedCount = 0;
        var totalUrls = _urls.Length;

        foreach (var url in _urls)
        {
            processedCount++;
            UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Processing URL {processedCount} of {totalUrls}...");

            try
            {
                var comments = await ProcessUrl(url);
                allComments.AddRange(comments);
            }
            catch (Exception ex)
            {
                // Log the error but continue processing other URLs
                Console.Error.WriteLine($"Error processing URL {url}: {ex.Message}");
            }
        }

        if (!allComments.Any())
        {
            throw new InvalidOperationException("No valid comments were found from the provided URLs");
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing feedback...");

        // Join all comments and analyze them
        var combinedComments = string.Join("\n---\n", allComments);
        var analysisResult = await AnalyzeComments("auto", combinedComments, allComments.Count);

        return (analysisResult, null);
    }

    private async Task<List<string>> ProcessUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return new List<string>();

        var service = ResolveFeedbackService(url);
        if (service != null)
        {
            var (markdown, _) = await service.GetFeedback();
            return new List<string> { markdown };
        }

        return new List<string>();
    }

    private IFeedbackService? ResolveFeedbackService(string url)
    {
        if (UrlParsing.IsYouTubeUrl(url))
        {
            var videoId = UrlParsing.ExtractYouTubeId(url);            if (!string.IsNullOrEmpty(videoId))
            {
                return _serviceProvider.CreateYouTubeService(videoId, string.Empty, OnStatusUpdate);
            }
        }
        else if (UrlParsing.IsGitHubUrl(url))
        {
            return _serviceProvider.CreateGitHubService(url, OnStatusUpdate);
        }
        else if (UrlParsing.IsRedditUrl(url))
        {
            var threadId = UrlParsing.ExtractRedditId(new[] { url });
            if (!string.IsNullOrEmpty(threadId))
            {
                return _serviceProvider.CreateRedditService(new[] { threadId }, OnStatusUpdate);
            }
        }        else if (UrlParsing.IsDevBlogsUrl(url))
        {
            return _serviceProvider.CreateDevBlogsService(url, OnStatusUpdate);
        }
        else if (UrlParsing.IsTwitterUrl(url))
        {
            var tweetId = TwitterUrlParser.ExtractTweetId(url);
            if (!string.IsNullOrEmpty(tweetId))
            {
                return _serviceProvider.CreateTwitterService(tweetId, OnStatusUpdate);
            }
        }
        else if (UrlParsing.IsBlueSkyUrl(url))
        {
            var postId = BlueSkyUrlParser.ExtractPostId(url);
            if (!string.IsNullOrEmpty(postId))
            {
                return _serviceProvider.CreateBlueSkyService(postId, OnStatusUpdate);
            }
        }
        else if (UrlParsing.IsHackerNewsUrl(url))
        {
            var itemId = UrlParsing.ExtractHackerNewsId(url);
            if (!string.IsNullOrEmpty(itemId))
            {
                return _serviceProvider.CreateHackerNewsService(itemId, OnStatusUpdate);
            }
        }
        
        return null;
    }
}
