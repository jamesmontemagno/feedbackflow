using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;

namespace FeedbackFunctions;

public class ContentFeedFunctions
{
    private readonly ILogger<ContentFeedFunctions> _logger;
    private readonly HackerNewsService _hnService;
    private readonly YouTubeService _ytService;
    private readonly RedditService _redditService;

    public ContentFeedFunctions(
        ILogger<ContentFeedFunctions> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        _hnService = new HackerNewsService();
        
        var ytApiKey = configuration["YouTube:ApiKey"];
        _ytService = new YouTubeService(ytApiKey ?? throw new InvalidOperationException("YouTube API key not configured"));

        var redditClientId = configuration["Reddit:ClientId"];
        var redditClientSecret = configuration["Reddit:ClientSecret"];
        _redditService = new RedditService(
            redditClientId ?? throw new InvalidOperationException("Reddit client ID not configured"),
            redditClientSecret ?? throw new InvalidOperationException("Reddit client secret not configured"));
    }

    [Function("GetRecentYouTubeVideos")]
    public async Task<HttpResponseData> GetRecentYouTubeVideos(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing recent YouTube videos request");

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
            var videos = await _ytService.SearchVideos(topic ?? "", tag ?? "", cutoffDate);
            
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
    public async Task<HttpResponseData> GetTrendingRedditThreads(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing trending Reddit threads request");

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
            var threads = await _redditService.GetSubredditThreads(subreddit, sortBy, cutoffDate);

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
    public async Task<HttpResponseData> SearchHackerNewsArticles(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Hacker News search request");

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            
            var keywords = queryParams["keywords"];
            if (string.IsNullOrEmpty(keywords))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("'keywords' parameter is required");
                return badResponse;
            }

            var keywordsList = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var matchingItems = await _hnService.SearchByTitle(keywordsList);

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
}