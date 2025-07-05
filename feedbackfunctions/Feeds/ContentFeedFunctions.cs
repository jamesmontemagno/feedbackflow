using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Services.Interfaces;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;
using SharedDump.Models.Account;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for retrieving content feeds from various platforms
/// </summary>
/// <remarks>
/// This class provides HTTP-triggered functions to fetch content feeds from
/// platforms like YouTube, Reddit, and Hacker News. It includes caching to
/// improve performance and reduce API calls.
/// </remarks>
public class ContentFeedFunctions
{
    private readonly ILogger<ContentFeedFunctions> _logger;
    private readonly IHackerNewsService _hnService;
    private readonly IYouTubeService _ytService;
    private readonly IRedditService _redditService;
    private readonly AuthenticationMiddleware _authMiddleware;
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Initializes a new instance of the ContentFeedFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="hackerNewsService">HackerNews service for story operations</param>
    /// <param name="youtubeService">YouTube service for video operations</param>
    /// <param name="redditService">Reddit service for thread operations</param>
    /// <param name="authMiddleware">Authentication middleware for request validation</param>
    public ContentFeedFunctions(
        ILogger<ContentFeedFunctions> logger,
        IHackerNewsService hackerNewsService,
        IYouTubeService youtubeService,
        IRedditService redditService,
        AuthenticationMiddleware authMiddleware)
    {
        _logger = logger;
        _hnService = hackerNewsService;
        _ytService = youtubeService;
        _redditService = redditService;
        _authMiddleware = authMiddleware;
    }

    /// <summary>
    /// Gets recent YouTube videos for content analysis
    /// </summary>
    /// <param name="req">HTTP request with query parameters</param>
    /// <returns>HTTP response with recent YouTube videos</returns>
    /// <remarks>
    /// Query parameters:
    /// - days: Required. Number of days of history to retrieve (integer)
    /// - query: Optional. Search query to filter videos
    /// - channelId: Optional. YouTube channel ID to filter videos
    /// - maxResults: Optional. Maximum number of results to return (default: 10)
    /// 
    /// Example usage:
    /// GET /api/GetRecentYouTubeVideos?days=7&amp;query=dotnet&amp;maxResults=20
    /// </remarks>
    [Function("GetRecentYouTubeVideos")]
    [Authorize]
    [UsageValidation(UsageType = SharedDump.Models.Account.UsageType.FeedQuery)]
    public async Task<HttpResponseData> GetRecentYouTubeVideos(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing recent YouTube videos request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            
            if (!int.TryParse(queryParams["days"], out var days))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("'days' parameter is required and must be an integer");
                return badResponse;
            }

            var topic = queryParams["topic"];
            var tag = queryParams["tag"];

            if (string.IsNullOrEmpty(topic) && string.IsNullOrEmpty(tag))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Either 'topic' or 'tag' parameter is required");
                return badResponse;
            }

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-days);
            var videos = await _ytService.SearchVideosBasicInfo(topic ?? "", tag ?? "", cutoffDate); // Changed method name
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(videos);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recent YouTube videos request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("GetTrendingRedditThreads")]
    [Authorize]
    [UsageValidation(UsageType = SharedDump.Models.Account.UsageType.FeedQuery)]
    public async Task<HttpResponseData> GetTrendingRedditThreads(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing trending Reddit threads request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            
            var subreddit = queryParams["subreddit"];
            if (string.IsNullOrEmpty(subreddit))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("'subreddit' parameter is required");
                return badResponse;
            }

            if (!int.TryParse(queryParams["days"], out var days))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("'days' parameter is required and must be an integer");
                return badResponse;
            }

            var sortBy = queryParams["sort"]?.ToLower() ?? "hot";
            if (!new[] { "hot", "top", "new" }.Contains(sortBy))
            {
                sortBy = "hot";
            }

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-days);
            var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, sortBy, cutoffDate); // Changed method name

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(threads);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing trending Reddit threads request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("SearchHackerNewsArticles")]
    [Authorize]
    [UsageValidation(UsageType = SharedDump.Models.Account.UsageType.FeedQuery)]
    public async Task<HttpResponseData> SearchHackerNewsArticles(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        [BlobInput("hackernews-cache/all.json", Connection = "ProductionStorage")] string? cachedBlob)
    {
        _logger.LogInformation("Processing Hacker News search request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            List<HackerNewsItemBasicInfo> matchingItems;

            if (!string.IsNullOrEmpty(cachedBlob))
            {
                // Use blob cache if available
                matchingItems = System.Text.Json.JsonSerializer.Deserialize<List<HackerNewsItemBasicInfo>>(cachedBlob) ?? new List<HackerNewsItemBasicInfo>();
                _logger.LogInformation("Blob cache hit");
            }
            else
            {
                matchingItems = await _hnService.SearchByTitleBasicInfo();
                _logger.LogInformation("Blob cache miss, fetched from service");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(matchingItems);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Hacker News search request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("CacheHackerNewsArticlesHourly")]
    [BlobOutput("hackernews-cache/all.json", Connection = "ProductionStorage")]
    public async Task<string> CacheHackerNewsArticlesHourly(
        [TimerTrigger("0 0 */2 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Starting hourly Hacker News cache refresh at: {Time}", DateTime.UtcNow);

        try
        {
            var items = await _hnService.SearchByTitleBasicInfo();
            _logger.LogInformation("Pre-cached Hacker News articles");
            // Serialize items to JSON for blob output
            return System.Text.Json.JsonSerializer.Serialize(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hourly Hacker News cache refresh");
            return string.Empty;
        }
    }
}
