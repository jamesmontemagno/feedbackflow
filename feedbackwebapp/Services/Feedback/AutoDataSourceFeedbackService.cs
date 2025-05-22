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
    
    private string GetServiceType(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "unknown";

        if (UrlParsing.IsYouTubeUrl(url))
            return "youtube";
        if (UrlParsing.IsRedditUrl(url))
            return "reddit";
        if (UrlParsing.IsGitHubUrl(url))
            return "github";
        if (UrlParsing.IsDevBlogsUrl(url))
            return "devblogs";
        if (UrlParsing.IsTwitterUrl(url))
            return "twitter";
        if (UrlParsing.IsBlueSkyUrl(url))
            return "bluesky";
        if (UrlParsing.IsHackerNewsUrl(url))
            return "hackernews";

        return "unknown";
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        if (_urls == null || _urls.Length == 0)
        {
            throw new InvalidOperationException("Please provide at least one URL to analyze");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Analyzing source URLs...");

        var allComments = new List<string>();
        var totalCommentCount = 0;
        var processedCount = 0;
        var totalUrls = _urls.Length;
        var servicesUsed = new HashSet<string>();

        foreach (var url in _urls)
        {
            processedCount++;
            UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Processing URL {processedCount} of {totalUrls}...");

            try
            {
                var serviceType = GetServiceType(url);
                servicesUsed.Add($"{serviceType}");

                var service = ResolveFeedbackService(url);
                if (service != null)
                {
                    var (comments, commentCount) = await GetCommentsFromUrl(url);
                    if (!string.IsNullOrWhiteSpace(comments))
                    {
                        allComments.Add(comments);
                        totalCommentCount += commentCount;
                    }
                }
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

        // Join all comments together and return total count along with services used
        var combinedComments = string.Join("\n---\n", allComments);
        return (combinedComments, totalCommentCount, servicesUsed.ToList().Distinct());
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing feedback...");
        
        // Calculate comment count if not provided
        int totalComments = commentCount ?? comments.Split("\n---\n").Length;
        
        // Determine service type based on additionalData
        string serviceType = "auto";
        if (additionalData is List<string> services && services.Count == 1)
        {
            serviceType = services[0];
        }
        
        // Analyze the combined comments
        var markdown = await AnalyzeCommentsInternal(serviceType, comments, totalComments);
        return (markdown, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments from all sources
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze all comments together
        return await AnalyzeComments(comments, commentCount, additionalData);
    }

    private async Task<(string comments, int commentCount)> GetCommentsFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return (string.Empty, 0);

        var service = ResolveFeedbackService(url);
        if (service != null)
        {
            // Get the raw comments and count
            var (rawComments, commentCount, _) = await service.GetComments();
            if (!string.IsNullOrWhiteSpace(rawComments))
            {
                return (rawComments, commentCount);
            }
        }

        return (string.Empty, 0);
    }

    private IFeedbackService? ResolveFeedbackService(string url) => _serviceProvider.GetService(url);
}
