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

        var sourceData = new List<FeedbackSourceData>();
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
                    var (comments, commentCount, additionalData) = await GetCommentsFromUrl(url);
                    if (!string.IsNullOrWhiteSpace(comments))
                    {
                        sourceData.Add(new FeedbackSourceData(serviceType, $"URL: {url}\n{comments}", commentCount, additionalData));
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

        if (!sourceData.Any())
        {
            throw new InvalidOperationException("No valid comments were found from the provided URLs");
        }

        // Join comments for each service and then all services together
        var allComments = sourceData
            .GroupBy(s => s.Source)
            .Select(g => $"# {char.ToUpper(g.Key[0]) + g.Key[1..]} Comments\n{string.Join("\n---\n", g.Select(s => s.Comments))}");
        var combinedComments = string.Join("\n\n", allComments);

        // Return the combined comments, total count, and the source data
        return (combinedComments, totalCommentCount, sourceData);
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

        // If we have the source data list, analyze based on number of URLs
        if (additionalData is List<FeedbackSourceData> sourceData)
        {
            // For a single URL, just analyze with the specific service type
            if (sourceData.Count == 1)
            {
                var data = sourceData[0];
                var markdown = await AnalyzeCommentsInternal(data.Source, data.Comments, data.CommentCount);
                return ($"# {char.ToUpper(data.Source[0]) + data.Source[1..]} Feedback Analysis\n\n{markdown}", sourceData);
            }

            // For multiple URLs, start with overall analysis then analyze each URL
            var analysisBySource = new List<string>();
            
            // First do the overall analysis of everything
            var allCommentsText = string.Join("\n---\n", sourceData.Select(s => s.Comments));
            var overallAnalysis = await AnalyzeCommentsInternal("auto", allCommentsText, totalComments);
            analysisBySource.Add("## Overall Analysis\n\n" + overallAnalysis);

            // Then analyze each source individually
            foreach (var data in sourceData)
            {
                var markdown = await AnalyzeCommentsInternal(data.Source, data.Comments, data.CommentCount);
                analysisBySource.Add($"## {char.ToUpper(data.Source[0]) + data.Source[1..]} Analysis\n\n{markdown}");
            }

            var combinedAnalysis = string.Join("\n\n", analysisBySource);
            return ($"# Multi-Source Feedback Analysis\n\n{combinedAnalysis}", sourceData);
        }
        
        // Fallback to regular analysis if we don't have the source data
        var defaultMarkdown = await AnalyzeCommentsInternal("auto", comments, totalComments);
        return (defaultMarkdown, additionalData);
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

    private async Task<(string comments, int commentCount, object? additionalData)> GetCommentsFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return (string.Empty, 0, null);

        var service = ResolveFeedbackService(url);
        if (service != null)
        {
            // Get the raw comments and count
            var (rawComments, commentCount, additionalData) = await service.GetComments();
            if (!string.IsNullOrWhiteSpace(rawComments))
            {
                return (rawComments, commentCount, additionalData);
            }
        }

        return (string.Empty, 0, null);
    }

    private IFeedbackService? ResolveFeedbackService(string url) => _serviceProvider.GetService(url);
}



public record FeedbackSourceData(string Source, string Comments, int CommentCount, object? AdditionalData);
