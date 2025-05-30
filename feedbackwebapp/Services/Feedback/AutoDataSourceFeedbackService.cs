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

        var commentsByService = new Dictionary<string, List<string>>();
        var totalCommentCount = 0;
        var processedCount = 0;
        var totalUrls = _urls.Length;

        foreach (var url in _urls)
        {
            processedCount++;
            UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Processing URL {processedCount} of {totalUrls}...");

            try
            {
                var serviceType = GetServiceType(url);
                var service = ResolveFeedbackService(url);
                if (service != null)
                {
                    var (comments, commentCount) = await GetCommentsFromUrl(url);
                    if (!string.IsNullOrWhiteSpace(comments))
                    {
                        if (!commentsByService.ContainsKey(serviceType))
                        {
                            commentsByService[serviceType] = new List<string>();
                        }
                        // Add comments from each URL separately, even if we already have comments from this service
                        commentsByService[serviceType].Add($"URL: {url}\n{comments}");
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

        if (!commentsByService.Any())
        {
            throw new InvalidOperationException("No valid comments were found from the provided URLs");
        }

        // Join comments for each service and then all services together
        var allComments = commentsByService.Select(kvp => 
            $"# {char.ToUpper(kvp.Key[0]) + kvp.Key[1..]} Comments\n{string.Join("\n---\n", kvp.Value)}"
        );
        var combinedComments = string.Join("\n\n", allComments);

        // Return the combined comments, total count, and the dictionary of service types to comments
        return (combinedComments, totalCommentCount, commentsByService);
    }    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing feedback...");
        
        // Calculate comment count if not provided
        int totalComments = commentCount ?? comments.Split("\n---\n").Length;

        // If we have the service dictionary, analyze based on number of URLs
        if (additionalData is Dictionary<string, List<string>> commentsByService)
        {
            // For a single URL, just analyze with the specific service type
            if (commentsByService.Count == 1 && commentsByService.First().Value.Count == 1)
            {
                var (serviceType, serviceComments) = commentsByService.First();
                var serviceCommentsText = serviceComments.First();
                var markdown = await AnalyzeCommentsInternal(serviceType, serviceCommentsText, 1);
                return ($"# {char.ToUpper(serviceType[0]) + serviceType[1..]} Feedback Analysis\n\n{markdown}", commentsByService);
            }

            // For multiple URLs, start with overall analysis then add per-service analysis
            var analysisByService = new List<string>();
            
            // First do the overall analysis of everything
            var allCommentsText = string.Join("\n---\n", commentsByService.SelectMany(kvp => kvp.Value));
            var overallAnalysis = await AnalyzeCommentsInternal("auto", allCommentsText, totalComments);
            analysisByService.Add("## Overall Analysis\n\n" + overallAnalysis);

            // Then analyze each service separately
            foreach (var (serviceType, serviceComments) in commentsByService)
            {
                var serviceCommentsText = string.Join("\n---\n", serviceComments);
                var markdown = await AnalyzeCommentsInternal(serviceType, serviceCommentsText, serviceComments.Count);
                analysisByService.Add($"## {char.ToUpper(serviceType[0]) + serviceType[1..]} Analysis\n\n{markdown}");
            }

            var combinedAnalysis = string.Join("\n\n", analysisByService);
            return ($"# Multi-Source Feedback Analysis\n\n{combinedAnalysis}", commentsByService);
        }
        
        // Fallback to regular analysis if we don't have the dictionary
        var defaultMarkdown = await AnalyzeCommentsInternal("auto", comments, totalComments);
        return (defaultMarkdown, null);
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
