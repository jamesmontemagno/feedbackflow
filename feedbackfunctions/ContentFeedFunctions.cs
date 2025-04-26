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
    private static readonly ConcurrentDictionary<string, (List<HackerNewsItemBasicInfo> Items, DateTime ExpirationTime)> _hnSearchCache = new();
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public ContentFeedFunctions(
        ILogger<ContentFeedFunctions> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
#if DEBUG

        _configuration = new ConfigurationBuilder()
                    .AddUserSecrets<Program>()
                    .Build();
#else

        _configuration = configuration;
#endif
        _logger = logger;
        
        _hnService = new HackerNewsService(httpClient);
        
        var ytApiKey = _configuration["YouTube:ApiKey"];
        _ytService = new YouTubeService(ytApiKey ?? throw new InvalidOperationException("YouTube API key not configured"), httpClient);

        var redditClientId = _configuration["Reddit:ClientId"];
        var redditClientSecret = _configuration["Reddit:ClientSecret"];
        _redditService = new RedditService(
            redditClientId ?? throw new InvalidOperationException("Reddit client ID not configured"),
            redditClientSecret ?? throw new InvalidOperationException("Reddit client secret not configured"), httpClient);
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

    private string GetCacheKey(string[] keywords) => 
        string.Join("-", keywords.OrderBy(k => k));

    private bool TryGetFromCache(string cacheKey, out List<HackerNewsItemBasicInfo>? items)
    {
        items = null;
        if (_hnSearchCache.TryGetValue(cacheKey, out var cacheEntry))
        {
            if (DateTime.UtcNow <= cacheEntry.ExpirationTime)
            {
                items = cacheEntry.Items;
                return true;
            }
            _hnSearchCache.TryRemove(cacheKey, out _);
        }
        return false;
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
            if (string.IsNullOrWhiteSpace(keywords))
            {
                keywords = "";
            }

            var keywordsList = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var cacheKey = GetCacheKey(keywordsList);

            List<HackerNewsItemBasicInfo> matchingItems;
            if (!TryGetFromCache(cacheKey, out var cachedItems))
            {
                matchingItems = await _hnService.SearchByTitleBasicInfo(keywordsList);
                _hnSearchCache.TryAdd(cacheKey, (matchingItems, DateTime.UtcNow.Add(_cacheDuration)));
                _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
            }
            else
            {
                matchingItems = cachedItems!;
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
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
}