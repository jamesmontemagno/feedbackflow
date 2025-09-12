using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Utils;

namespace FeedbackWebApp.Services.Feedback;

public class AutoDataSourceFeedbackService: FeedbackService, IAutoDataSourceFeedbackService, ICachableFeedbackService
{
    private readonly string[] _urls;
    private readonly FeedbackServiceProvider _serviceProvider;
    private bool _includeIndividualReports = false; // Default to false - only full report
    
    /// <summary>
    /// Last fetched comments snapshot for caching/reanalysis support
    /// </summary>
    public (string comments, int commentCount, object? additionalData)? LastCommentsSnapshot { get; private set; }

    public AutoDataSourceFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authenticationHeaderService,
        FeedbackServiceProvider serviceProvider,
        string[] urls,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate)
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
        var result = (combinedComments, totalCommentCount, sourceData);
        
        // Cache the snapshot for potential reanalysis
        LastCommentsSnapshot = (combinedComments, totalCommentCount, (object?)sourceData);
        
        return result;
    }   
    
    public void SetIncludeIndividualReports(bool includeIndividualReports)
    {
        _includeIndividualReports = includeIndividualReports;
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
                UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
                return ($"# {char.ToUpper(data.Source[0]) + data.Source[1..]} Feedback Analysis\n\n{markdown}", sourceData);
            }

            // For multiple URLs, start with overall analysis, then optionally analyze each URL individually
            var analysisBySource = new List<string>();
            
            // First do the overall analysis of everything
            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Creating overall analysis...");
            var allCommentsText = string.Join("\n---\n", sourceData.Select(s => s.Comments));
            var overallAnalysis = await AnalyzeCommentsInternalWithoutStatusUpdate("auto", allCommentsText, totalComments);
            analysisBySource.Add("## Overall Analysis\n\n" + overallAnalysis);

            // Only analyze each source individually if the user requested it
            if (_includeIndividualReports)
            {
                for (int i = 0; i < sourceData.Count; i++)
                {
                    var data = sourceData[i];
                    UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {char.ToUpper(data.Source[0]) + data.Source[1..]} feedback ({i + 1}/{sourceData.Count})...");
                    var markdown = await AnalyzeCommentsInternalWithoutStatusUpdate(data.Source, data.Comments, data.CommentCount);
                    analysisBySource.Add($"## {char.ToUpper(data.Source[0]) + data.Source[1..]} Analysis\n\n{markdown}");
                }
                UpdateStatus(FeedbackProcessStatus.Completed, "Multi-source analysis completed");
                var combinedAnalysis = string.Join("\n\n", analysisBySource);
                return ($"# Multi-Source Feedback Analysis\n\n{combinedAnalysis}", sourceData);
            }
            else
            {
                UpdateStatus(FeedbackProcessStatus.Completed, "Overall analysis completed");
                return ($"# Multi-Source Feedback Analysis\n\n{overallAnalysis}", sourceData);
            }
        }
        
        // Fallback to regular analysis if we don't have the source data
        var defaultMarkdown = await AnalyzeCommentsInternal("auto", comments, totalComments);
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return (defaultMarkdown, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments from all sources
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments found");
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze all comments together
        var result = await AnalyzeComments(comments, commentCount, additionalData);
        
        // Ensure completion status is set
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed successfully");
        
        return result;
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
