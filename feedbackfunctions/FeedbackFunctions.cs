using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.DevBlogs;
using SharedDump.AI;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Json;
using SharedDump.Services;
using SharedDump.Services.Interfaces;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions;

/// <summary>
/// Azure Functions for retrieving and analyzing feedback from various platforms
/// </summary>
/// <remarks>
/// This class contains HTTP-triggered functions that interact with different content services
/// to retrieve and analyze feedback from platforms like GitHub, YouTube, Reddit, etc.
/// </remarks>
public class FeedbackFunctions
{
    private readonly ILogger<FeedbackFunctions> _logger;
    private readonly IGitHubService _githubService;
    private readonly IHackerNewsService _hnService;
    private readonly IYouTubeService _ytService;
    private readonly IRedditService _redditService;
    private readonly IDevBlogsService _devBlogsService;
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly ITwitterService _twitterService;
    private readonly IBlueSkyService _blueSkyService;
    private readonly AuthenticationMiddleware _authMiddleware;

    /// <summary>
    /// Initializes a new instance of the FeedbackFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="githubService">GitHub service for repository operations</param>
    /// <param name="youtubeService">YouTube service for video operations</param>
    /// <param name="hackerNewsService">HackerNews service for story operations</param>
    /// <param name="redditService">Reddit service for thread operations</param>
    /// <param name="devBlogsService">DevBlogs service for article operations</param>
    /// <param name="analyzerService">Feedback analyzer service for AI-powered analysis</param>
    /// <param name="twitterService">Twitter service for tweet operations</param>
    /// <param name="blueSkyService">BlueSky service for post operations</param>
    public FeedbackFunctions(
        ILogger<FeedbackFunctions> logger, 
        IGitHubService githubService, 
        IYouTubeService youtubeService, 
        IHackerNewsService hackerNewsService, 
        IRedditService redditService, 
        IDevBlogsService devBlogsService, 
        IFeedbackAnalyzerService analyzerService,
        ITwitterService twitterService,
        IBlueSkyService blueSkyService,
        AuthenticationMiddleware authMiddleware)
    {
        _logger = logger;
        _githubService = githubService;
        _ytService = youtubeService;
        _hnService = hackerNewsService;
        _redditService = redditService;
        _devBlogsService = devBlogsService;
        _analyzerService = analyzerService;
        _twitterService = twitterService;
        _blueSkyService = blueSkyService;
        _authMiddleware = authMiddleware;
    }

    /// <summary>
    /// Gets GitHub feedback (issues, pull requests, or discussions) based on a GitHub URL
    /// </summary>
    /// <param name="req">HTTP request containing the GitHub URL</param>
    /// <returns>GitHub feedback data in JSON format</returns>
    /// <remarks>
    /// Parameters:
    /// - url: Required. GitHub URL (issue, PR, discussion, or repository)
    /// - maxComments: Optional. Maximum number of comments to return (default: 100)
    /// 
    /// Example usage:
    /// GET /api/GetGitHubFeedback?url=https://github.com/dotnet/maui/issues/123&maxComments=50
    /// </remarks>
    [Function("GetGitHubFeedback")]
    [Authorize]
    public async Task<HttpResponseData> GetGitHubFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GitHub feedback request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var url = queryParams["url"];
        var maxCommentsStr = queryParams["maxComments"];

        if (string.IsNullOrEmpty(url))
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("'url' parameter is required");
            return response;
        }

        var maxComments = int.TryParse(maxCommentsStr, out var max) ? max : 100;

        try
        {
            // Parse the GitHub URL to determine what type of content to fetch
            var urlInfo = SharedDump.Utils.GitHubUrlParser.ParseGitHubUrl(url);
            if (urlInfo == null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid GitHub URL provided");
                return response;
            }

            // Validate repository exists
            if (!await _githubService.CheckRepositoryValid(urlInfo.Owner, urlInfo.Repository))
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Invalid repository");
                return response;
            }

            object result;

            switch (urlInfo.Type)
            {
                case SharedDump.Utils.GitHubUrlType.Issue:
                    if (urlInfo.Number.HasValue)
                    {
                        var issueModel = await _githubService.GetIssueWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value);
                        
                        if (issueModel == null)
                        {
                            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFoundResponse.WriteStringAsync("Issue not found");
                            return notFoundResponse;
                        }

                        // Limit comments to maxComments
                        if (issueModel.Comments.Length > maxComments)
                        {
                            issueModel.Comments = issueModel.Comments.Take(maxComments).ToArray();
                        }

                        result = new[] { issueModel };
                    }
                    else
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteStringAsync("Issue number is required in the URL");
                        return badResponse;
                    }
                    break;

                case SharedDump.Utils.GitHubUrlType.PullRequest:
                    if (urlInfo.Number.HasValue)
                    {
                        var prModel = await _githubService.GetPullRequestWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value);
                        
                        if (prModel == null)
                        {
                            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFoundResponse.WriteStringAsync("Pull request not found");
                            return notFoundResponse;
                        }

                        // Limit comments to maxComments
                        if (prModel.Comments.Length > maxComments)
                        {
                            prModel.Comments = prModel.Comments.Take(maxComments).ToArray();
                        }

                        result = new[] { prModel };
                    }
                    else
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteStringAsync("Pull request number is required in the URL");
                        return badResponse;
                    }
                    break;

                case SharedDump.Utils.GitHubUrlType.Discussion:
                    if (urlInfo.Number.HasValue)
                    {
                        var discussionModel = await _githubService.GetDiscussionWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value);
                        
                        if (discussionModel == null)
                        {
                            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                            await notFoundResponse.WriteStringAsync("Discussion not found");
                            return notFoundResponse;
                        }

                        // Limit comments to maxComments
                        if (discussionModel.Comments.Length > maxComments)
                        {
                            discussionModel.Comments = discussionModel.Comments.Take(maxComments).ToArray();
                        }

                        result = new[] { discussionModel };
                    }
                    else
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteStringAsync("Discussion number is required in the URL");
                        return badResponse;
                    }
                    break;

                default:
                    var invalidResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await invalidResponse.WriteStringAsync("URL must be a specific GitHub issue, pull request, or discussion");
                    return invalidResponse;
            }

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(result);
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub feedback request for URL: {Url}", url);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    /// <summary>
    /// Retrieves feedback from Hacker News stories and comments
    /// </summary>
    /// <param name="req">HTTP request containing query parameters</param>
    /// <returns>HTTP response with Hacker News data</returns>
    /// <remarks>
    /// Query parameters:
    /// - ids: Required. Comma-separated list of Hacker News story IDs
    /// 
    /// Example usage:
    /// GET /api/GetHackerNewsFeedback?ids=123456,789012
    /// </remarks>
    [Function("GetHackerNewsFeedback")]
    public async Task<HttpResponseData> GetHackerNewsFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing HackerNews feedback request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var idsParam = queryParams["ids"];

        if (string.IsNullOrEmpty(idsParam))
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("'ids' parameter is required");
            return response;
        }

        try
        {
            var ids = idsParam.Split(',').Select(int.Parse).ToArray();
            var results = new List<IAsyncEnumerable<HackerNewsItem>>();

            foreach (var id in ids)
            {
                var item = await _hnService.GetItemData(id);
                if (item?.Title != null)
                {
                    results.Add(_hnService.GetItemWithComments(id));
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HackerNews feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("GetYouTubeFeedback")]
    public async Task<HttpResponseData> GetYouTubeFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing YouTube feedback request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var videoIds = queryParams["videos"]?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var channelId = queryParams["channel"];
        var playlistIds = queryParams["playlists"]?.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if ((videoIds == null || videoIds.Length == 0) && 
            string.IsNullOrEmpty(channelId) && 
            (playlistIds == null || playlistIds.Length == 0))
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("At least one of 'videos', 'channel', or 'playlists' parameters is required");
            return response;
        }

        try
        {
            var allVideoIds = new List<string>();

            if (videoIds != null)
            {
                allVideoIds.AddRange(videoIds);
            }

            if (channelId != null)
            {
                var channelVideoIds = await _ytService.GetChannelVideos(channelId);
                allVideoIds.AddRange(channelVideoIds);
            }

            if (playlistIds != null)
            {
                foreach (var playlistId in playlistIds)
                {
                    var playlistVideoIds = await _ytService.GetPlaylistVideos(playlistId);
                    allVideoIds.AddRange(playlistVideoIds);
                }
            }

            var outputVideos = new List<YouTubeOutputVideo>();

            foreach (var videoId in allVideoIds)
            {
                var video = await _ytService.ProcessVideo(videoId);
                outputVideos.Add(video);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(outputVideos);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing YouTube feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("GetRedditFeedback")]
    public async Task<HttpResponseData> GetRedditFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Reddit feedback request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var threadIds = queryParams["threads"]?.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (threadIds == null || threadIds.Length == 0)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("'threads' parameter is required");
            return response;
        }

        try
        {
            var threadResults = new List<RedditThreadModel>();

            foreach (var threadId in threadIds)
            {
                if (SharedDump.Utils.RedditUrlParser.IsRedditShortUrl(threadId))
                {
                    var resolvedId = await SharedDump.Utils.RedditUrlParser.GetShortlinkIdAsync2(threadId);
                    if (!string.IsNullOrWhiteSpace(resolvedId))
                    {
                        threadResults.Add(await _redditService.GetThreadWithComments(resolvedId));
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning("Could not resolve Reddit shortlink: {ThreadId}", threadId);
                        continue;
                    }
                }
                var thread = await _redditService.GetThreadWithComments(threadId);
                threadResults.Add(thread);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(threadResults);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Reddit feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("GetDevBlogsFeedback")]
    public async Task<HttpResponseData> GetDevBlogsFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing DevBlogs feedback request");
        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var articleUrl = queryParams["articleUrl"];
            if (string.IsNullOrWhiteSpace(articleUrl))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("'articleUrl' parameter is required");
                return badResponse;
            }

            var result = await _devBlogsService.FetchArticleWithCommentsAsync(articleUrl);
            if (result == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Could not fetch or parse DevBlogs article or comments.");
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing DevBlogs feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    /// <summary>
    /// Analyzes comments using AI to provide insights and summaries
    /// </summary>
    /// <param name="req">HTTP request with comment data to analyze</param>
    /// <returns>HTTP response with markdown-formatted analysis</returns>
    /// <remarks>
    /// The request body should be a JSON object with the following properties:
    /// - comments: Required. The text content to analyze
    /// - serviceType: Required. The source platform (e.g., "github", "youtube", "manual")
    /// - customPrompt: Optional. Custom system prompt to use for analysis
    /// 
    /// Example usage:
    /// ```json
    /// {
    ///   "comments": "This is the content to analyze...",
    ///   "serviceType": "github",
    ///   "customPrompt": "Optional custom prompt to use"
    /// }
    /// ```
    /// </remarks>
    [Function("AnalyzeComments")]
    public async Task<HttpResponseData> AnalyzeComments(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing comment analysis request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize(requestBody, FeedbackJsonContext.Default.AnalyzeCommentsRequest);

            if (string.IsNullOrEmpty(request?.Comments))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Comments JSON is required");
                return badResponse;
            }

            var analysisBuilder = new System.Text.StringBuilder();
            await foreach (var update in _analyzerService.GetStreamingAnalysisAsync(
                request.ServiceType, 
                request.Comments,
                request.CustomPrompt))
            {
                analysisBuilder.Append(update);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(analysisBuilder.ToString());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing comment analysis request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("AnalyzeCommentsBYOK")]
    public async Task<HttpResponseData> AnalyzeCommentsBYOK(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing BYOK comment analysis request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize(requestBody, FeedbackJsonContext.Default.AnalyzeCommentsBYOKRequest);

            if (string.IsNullOrEmpty(request?.Comments))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Comments JSON is required");
                return badResponse;
            }

            if (string.IsNullOrEmpty(request.Endpoint) || string.IsNullOrEmpty(request.ApiKey) || string.IsNullOrEmpty(request.Deployment))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Azure OpenAI configuration (endpoint, apiKey, deployment) is required");
                return badResponse;
            }

            var analyzerService = new FeedbackAnalyzerService(request.Endpoint, request.ApiKey, request.Deployment);
            
            var analysisBuilder = new System.Text.StringBuilder();
            await foreach (var update in analyzerService.GetStreamingAnalysisAsync(
                request.ServiceType, 
                request.Comments,
                request.SystemPrompt))
            {
                analysisBuilder.Append(update);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(analysisBuilder.ToString());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BYOK comment analysis request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

   
    [Function("GetTwitterFeedback")]
    public async Task<HttpResponseData> GetTwitterFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Twitter/X feedback request");
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var tweetUrlOrId = queryParams["tweet"];
        if (string.IsNullOrWhiteSpace(tweetUrlOrId))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("'tweet' parameter is required");
            return badResponse;
        }
        try
        {
            var result = await _twitterService.GetTwitterThreadAsync(tweetUrlOrId);
            if (result == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Could not fetch or parse Twitter feedback.");
                return notFound;
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            var json = System.Text.Json.JsonSerializer.Serialize(result, TwitterFeedbackJsonContext.Default.TwitterFeedbackResponse);
            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Twitter feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    [Function("GetBlueSkyFeedback")]
    public async Task<HttpResponseData> GetBlueSkyFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing BlueSky feedback request");
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var postUrlOrId = queryParams["post"];
        
        if (string.IsNullOrWhiteSpace(postUrlOrId))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("'post' parameter is required");
            return badResponse;
        }
        
        try
        {
            var result = await _blueSkyService.GetBlueSkyPostAsync(postUrlOrId);
            if (result == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Could not fetch or parse BlueSky feedback.");
                return notFound;
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            var json = System.Text.Json.JsonSerializer.Serialize(result, BlueSkyFeedbackJsonContext.Default.BlueSkyFeedbackResponse);
            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BlueSky feedback request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }
    

}

public class AnalyzeCommentsRequest
{
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;
    
    [JsonPropertyName("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [JsonPropertyName("customPrompt")]
    public string? CustomPrompt { get; set; }
}

public class AnalyzeCommentsBYOKRequest
{
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;
    
    [JsonPropertyName("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("deployment")]
    public string Deployment { get; set; } = string.Empty;

    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; set; }
}
