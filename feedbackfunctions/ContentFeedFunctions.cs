using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using System.Collections.Concurrent;

namespace FeedbackFunctions;

public class ContentFeedFunctions
{
    private readonly ILogger<ContentFeedFunctions> _logger;
    private readonly IConfiguration _configuration;
    private readonly HackerNewsService _hnService;
    private readonly YouTubeService _ytService;
    private readonly RedditService _redditService;
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    public ContentFeedFunctions(
        ILogger<ContentFeedFunctions> logger,
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
        
        _hnService = new HackerNewsService(httpClientFactory.CreateClient("HackerNews"));
        
        var ytApiKey = _configuration["YouTube:ApiKey"];
        _ytService = new YouTubeService(ytApiKey ?? throw new InvalidOperationException("YouTube API key not configured"), httpClientFactory.CreateClient("YouTube"));

        var redditClientId = _configuration["Reddit:ClientId"];
        var redditClientSecret = _configuration["Reddit:ClientSecret"];
        _redditService = new RedditService(
            redditClientId ?? throw new InvalidOperationException("Reddit client ID not configured"),
            redditClientSecret ?? throw new InvalidOperationException("Reddit client secret not configured"), httpClientFactory.CreateClient("Reddit"));
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

#if DEBUG && BLOB_FUNCTIONS

    [Function("SearchHackerNewsArticles")]
    public async Task<HttpResponseData> SearchHackerNewsArticles(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        [BlobInput("hackernews-cache/all.json")] string? cachedBlob)
    {
        _logger.LogInformation("Processing Hacker News search request");

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
    [BlobOutput("hackernews-cache/all.json")]
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
#endif
}
