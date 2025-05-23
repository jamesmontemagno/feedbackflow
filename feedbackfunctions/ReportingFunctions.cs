using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Reddit;
using SharedDump.AI;
using SharedDump.Utils;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for generating reports based on feedback data
/// </summary>
/// <remarks>
/// This class contains functions that generate comprehensive reports
/// by analyzing feedback from various platforms. The reports are typically
/// formatted in markdown and may include sentiment analysis, trends, and key insights.
/// </remarks>
public class ReportingFunctions
{
    private readonly ILogger<ReportingFunctions> _logger;
    private readonly RedditService _redditService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the ReportingFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="httpClientFactory">HTTP client factory for creating named clients</param>
    public ReportingFunctions(
        ILogger<ReportingFunctions> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {

#if DEBUG

        _configuration = new ConfigurationBuilder()
                    .AddJsonFile("local.settings.json")
                    .AddUserSecrets<Program>()
                    .Build();
#else

        _configuration = configuration;
#endif
        _logger = logger;

        var redditClientId = _configuration["Reddit:ClientId"] ?? throw new InvalidOperationException("Reddit client ID not configured");
        var redditClientSecret = _configuration["Reddit:ClientSecret"] ?? throw new InvalidOperationException("Reddit client secret not configured");
        _redditService = new RedditService(redditClientId, redditClientSecret, httpClientFactory.CreateClient("Reddit"));

        var endpoint = _configuration["Azure:OpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = _configuration["Azure:OpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key not configured");
        var deployment = _configuration["Azure:OpenAI:Deployment"] ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured");
        _analyzerService = new FeedbackAnalyzerService(endpoint, apiKey, deployment);
    }

    /// <summary>
    /// Generates a comprehensive report of a subreddit's threads and comments
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with a markdown-formatted report</returns>
    /// <remarks>
    /// Query parameters:
    /// - subreddit: Required. The name of the subreddit to analyze
    /// - days: Optional. Number of days of history to analyze (default: 7)
    /// - limit: Optional. Maximum number of threads to analyze (default: 25)
    /// - sort: Optional. Sort method for threads ("hot", "new", "top", etc.)
    /// 
    /// The function fetches Reddit threads, analyzes their content using AI,
    /// and returns a markdown-formatted report with insights, trends, and key takeaways.
    /// </remarks>
    /// <example>
    /// GET /api/RedditReport?subreddit=dotnet&amp;days=30&amp;limit=50&amp;sort=top
    /// </example>
    [Function("RedditReport")]
    public async Task<HttpResponseData> RedditReport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting Reddit Report processing for URL {RequestUrl}", req.Url);

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var subreddit = queryParams["subreddit"];

        if (string.IsNullOrEmpty(subreddit))
        {
            _logger.LogWarning("Reddit Report request rejected - missing subreddit parameter");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Subreddit parameter is required");
            return response;
        }

        try
        {
            _logger.LogInformation("Fetching threads for subreddit r/{Subreddit}", subreddit);
            
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-7);
            _logger.LogDebug("Using cutoff date of {CutoffDate} for thread retrieval", cutoffDate);
            
            var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, "hot", cutoffDate);
            _logger.LogInformation("Retrieved {ThreadCount} threads from r/{Subreddit}", threads.Count, subreddit);

            var topThreads = threads
                .OrderByDescending(t => (t.NumComments * 0.7) + (t.Score * 0.3))
                .Take(5)
                .ToList();
            _logger.LogInformation("Selected top {TopThreadCount} threads for detailed analysis", topThreads.Count);

            // Create tasks for parallel processing of each thread
            var threadTasks = topThreads.Select(async thread =>
            {
                _logger.LogDebug("Fetching full thread details for thread {ThreadId}: {ThreadTitle}", thread.Id, thread.Title);
                var fullThread = await _redditService.GetThreadWithComments(thread.Id);
                
                var flatComments = FlattenComments(fullThread.Comments);

                _logger.LogInformation("Thread {ThreadId} has {CommentCount} comments", thread.Id, flatComments.Count);
                
                var threadContent = $"Title: {fullThread.Title}\n\nContent: {fullThread.SelfText}\n\nComments:\n";
                threadContent += string.Join("\n", flatComments.Select(c => $"Comment by {c.Author}: {c.Body}"));

                var customPrompt = @"Analyze this Reddit thread and provide a concise summary in 3 short sections with use of emojis throughotu to add visual flair:

1. Executive Summary: Brief overview of the main topic and key points (1-3 sentences)
2. Key Insights: Most important takeaways or findings (2-4 bullet points)
3. Sentiment Analysis: Overall mood of the discussion (1-2 sentence2)

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";

                _logger.LogDebug("Analyzing thread {ThreadId}", thread.Id);
                var threadAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", threadContent, customPrompt);
              
                _logger.LogDebug("Completed analysis for thread {ThreadId}", thread.Id);

                return (Thread: fullThread, Analysis: threadAnalysis, Comments: flatComments);
            }).ToList();

            // Wait for all thread processing to complete
            var results = await Task.WhenAll(threadTasks);

            var threadAnalyses = results.Select(r => (r.Thread, r.Analysis)).ToList();
            var allComments = results.SelectMany(r => r.Comments).ToList();

            _logger.LogInformation("Processing top comments from {TotalCommentCount} total comments", allComments.Count);
            
            // Enhance top comments with thread information
            var topComments = allComments
                .OrderByDescending(c => c.Score)
                .Take(5)
                .Select(comment =>
                {
                    // Find the parent thread for this comment
                    var parentThread = results.First(r => r.Comments.Any(c => c.Id == comment.Id)).Thread;
                    return new TopCommentInfo(
                        Comment: comment,
                        Thread: parentThread,
                        CommentUrl: $"https://reddit.com{parentThread.Permalink}{comment.Id}"
                    );
                })
                .ToList();

            _logger.LogInformation("Generating weekly summary analysis for r/{Subreddit}", subreddit);

            var customWeeklyContentPrompt = $@"Analyze all of the top posts of the last week of Reddit discussions from r/{subreddit} this is a list of all of the comment sfrom all of these top threads of various topics. provide a concise summary in 3 short sections with use of emojis to add visual flair througout:

1. Executive Summary: Brief overview of the main topic and key points (1-3 sentences)
2. Key Insights or Trends: Most important takeaways or findings (2-4 bullet points)
3. Sentiment Analysis: Overall mood of the discussion (1-2 sentence)

Keep each section very brief and focused. Total analysis should be no more than three short paragraphs. Format in markdown.";
            var weeklyContent = string.Join("\n\n", allComments.Select(c => c.Body));

            var weeklyAnalysis = await _analyzerService.AnalyzeCommentsAsync("reddit", weeklyContent, customWeeklyContentPrompt);
            _logger.LogDebug("Weekly analysis completed");

            _logger.LogInformation("Generating HTML email report");
            var emailHtml = EmailUtils.GenerateRedditReportEmail(
                subreddit, 
                cutoffDate, 
                weeklyAnalysis, 
                threadAnalyses, 
                topComments);

            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Reddit Report processing completed for r/{Subreddit} in {ProcessingTime:c}. Analyzed {ThreadCount} threads and {CommentCount} comments", 
                subreddit, processingTime, topThreads.Count, allComments.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(emailHtml);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit report for r/{Subreddit}. Processing time: {ProcessingTime:c}", 
                subreddit, DateTime.UtcNow - startTime);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error processing report: {ex.Message}");
            return response;
        }
    }

    private static List<RedditCommentModel> FlattenComments(List<RedditCommentModel> comments)
    {
        var flattened = new List<RedditCommentModel>();
        foreach (var comment in comments)
        {
            flattened.Add(comment);
            if (comment.Replies?.Any() == true)
            {
                flattened.AddRange(FlattenComments(comment.Replies));
            }
        }
        return flattened;
    }
}
