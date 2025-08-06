using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Utils;
using SharedDump.Models.Account;

namespace FeedbackFunctions.FeedbackAnalysis;

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
    private readonly IUserAccountService _userAccountService;

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
        AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService)
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
        _userAccountService = userAccountService;
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

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, url);
            
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
    [Authorize]
    public async Task<HttpResponseData> GetHackerNewsFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing HackerNews feedback request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, idsParam);
            
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
    [Authorize]
    public async Task<HttpResponseData> GetYouTubeFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing YouTube feedback request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

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
            
            // Track usage on successful completion - include original parameters and processed video IDs
            var trackingInfo = new List<string>();
            if (videoIds != null && videoIds.Length > 0)
                trackingInfo.Add($"videos={string.Join(",", videoIds)}");
            if (!string.IsNullOrEmpty(channelId))
                trackingInfo.Add($"channel={channelId}");
            if (playlistIds != null && playlistIds.Length > 0)
                trackingInfo.Add($"playlists={string.Join(",", playlistIds)}");
            if (allVideoIds.Count > 0)
                trackingInfo.Add($"processed_videos={string.Join(",", allVideoIds)}");
            
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, string.Join("; ", trackingInfo));
            
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
    [Authorize]
    public async Task<HttpResponseData> GetRedditFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Reddit feedback request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

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
            
            // Track usage on successful completion - include original thread IDs
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, $"threads={string.Join(",", threadIds)}");
            
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
    [Authorize]
    public async Task<HttpResponseData> GetDevBlogsFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing DevBlogs feedback request");
        
        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;
            
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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, articleUrl);
            
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
    [Authorize]
    public async Task<HttpResponseData> AnalyzeComments(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing comment analysis request");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.Analysis, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;

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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.Analysis, _userAccountService, _logger, request.ServiceType);
            
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


    [Function("GetTwitterFeedback")]
    [Authorize]
    public async Task<HttpResponseData> GetTwitterFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Twitter/X feedback request");
        
        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Check if user's tier supports Twitter/X access
        var userAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
        if (userAccount != null && !SharedDump.Utils.Account.AccountTierUtils.SupportsTwitterAccess(userAccount.Tier))
        {
            var tierResponse = req.CreateResponse(HttpStatusCode.Forbidden);
            await tierResponse.WriteAsJsonAsync(new
            {
                ErrorCode = "TWITTER_ACCESS_DENIED",
                Message = "Twitter/X access is available for Pro, Pro+, and Admin users only. Please upgrade your account to access this feature.",
                RequiredTier = "Pro",
                CurrentTier = userAccount.Tier.ToString(),
                UpgradeUrl = "/account-settings"
            });
            return tierResponse;
        }

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;
            
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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, tweetUrlOrId);
            
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
    [Authorize]
    public async Task<HttpResponseData> GetBlueSkyFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing BlueSky feedback request");
        
        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        // Validate usage limits
        var usageValidationResponse = await req.ValidateUsageAsync(user!, UsageType.FeedQuery, _userAccountService, _logger);
        if (usageValidationResponse != null)
            return usageValidationResponse;
            
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
            
            // Track usage on successful completion
            await user!.TrackUsageAsync(UsageType.FeedQuery, _userAccountService, _logger, postUrlOrId);
            
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

    /// <summary>
    /// Automatically analyzes feedback from any supported platform based on a single URL
    /// </summary>
    /// <param name="req">HTTP request containing the URL to analyze</param>
    /// <returns>HTTP response with markdown-formatted analysis</returns>
    /// <remarks>
    /// Parameters:
    /// - url: Required. URL from any supported platform (GitHub, YouTube, Reddit, DevBlogs, Twitter, BlueSky, HackerNews)
    /// - maxComments: Optional. Maximum number of comments to analyze (default: 100)
    /// - customPrompt: Optional. Custom analysis prompt to use
    /// 
    /// Example usage:
    /// GET /api/AutoAnalyze?url=https://github.com/dotnet/maui/issues/123&maxComments=50
    /// GET /api/AutoAnalyze?url=https://www.youtube.com/watch?v=VIDEO_ID
    /// GET /api/AutoAnalyze?url=https://www.reddit.com/r/programming/comments/POST_ID/
    /// </remarks>
    [Function("AutoAnalyze")]
    public async Task<HttpResponseData> AutoAnalyze(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Processing auto-analyze request");
            _logger.LogInformation("Request URL: {Url}", req.Url.ToString());
            _logger.LogInformation("Request method: {Method}", req.Method);
            _logger.LogInformation("Request headers count: {Count}", req.Headers.Count());

            // Log query parameters for debugging
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            _logger.LogInformation("Query parameters: url={Url}, maxComments={MaxComments}, customPrompt={CustomPrompt}, apikey={ApiKeyPresent}", 
                queryParams["url"], 
                queryParams["maxComments"], 
                queryParams["customPrompt"],
                !string.IsNullOrEmpty(queryParams["apikey"]) ? "Present" : "Not present");

            _logger.LogInformation("Starting API key validation...");

            // Get the required services with error handling
            IApiKeyService apiKeyService;
            try
            {
                _logger.LogInformation("Getting IApiKeyService from dependency injection...");
                apiKeyService = req.FunctionContext.InstanceServices.GetRequiredService<IApiKeyService>();
                _logger.LogInformation("Successfully obtained IApiKeyService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get IApiKeyService from dependency injection");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Service configuration error.");
                return errorResponse;
            }

            // Validate API key and check usage limits (AutoAnalyze = 1 usage point)
            var (isValid, errorResponse2, userId) = await ApiKeyValidationHelper.ValidateApiKeyWithUsageAsync(req, 
                apiKeyService,
                _userAccountService,
                _logger,
                1);
            
            
            if (!isValid)
            {
                _logger.LogWarning("API key validation failed");
                return errorResponse2!;
            }

            _logger.LogInformation("API key validation successful, user ID: {UserId}", userId);

            var url = queryParams["url"];
            var maxCommentsStr = queryParams["maxComments"];
            var customPrompt = queryParams["customPrompt"];

            if (string.IsNullOrEmpty(url))
            {
                _logger.LogWarning("URL parameter is missing or empty");
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("'url' parameter is required");
                return response;
            }

            _logger.LogInformation("Processing URL: {Url}", url);

            var maxComments = int.TryParse(maxCommentsStr, out var max) ? max : 1000;
            _logger.LogInformation("Max comments: {MaxComments}", maxComments);

            string serviceType;
            object platformData;
            string commentsText;

            _logger.LogInformation("Determining platform type for URL: {Url}", url);

            // Determine platform and get data
            if (SharedDump.Utils.UrlParsing.IsGitHubUrl(url))
            {
                _logger.LogInformation("Detected GitHub URL");
                serviceType = "github";
                platformData = await GetGitHubDataForAnalysis(url, maxComments);
                commentsText = ConvertGitHubDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsYouTubeUrl(url))
            {
                _logger.LogInformation("Detected YouTube URL");
                serviceType = "youtube";
                platformData = await GetYouTubeDataForAnalysis(url, maxComments);
                commentsText = ConvertYouTubeDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsRedditUrl(url))
            {
                _logger.LogInformation("Detected Reddit URL");
                serviceType = "reddit";
                platformData = await GetRedditDataForAnalysis(url, maxComments);
                commentsText = ConvertRedditDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsDevBlogsUrl(url))
            {
                _logger.LogInformation("Detected DevBlogs URL");
                serviceType = "devblogs";
                platformData = await GetDevBlogsDataForAnalysis(url, maxComments);
                commentsText = ConvertDevBlogsDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsTwitterUrl(url))
            {
                _logger.LogInformation("Detected Twitter URL");
                serviceType = "twitter";
                
                platformData = await GetTwitterDataForAnalysis(url, maxComments);
                commentsText = ConvertTwitterDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsBlueSkyUrl(url))
            {
                _logger.LogInformation("Detected BlueSky URL");
                serviceType = "bluesky";
                platformData = await GetBlueSkyDataForAnalysis(url, maxComments);
                commentsText = ConvertBlueSkyDataToCommentsText(platformData);
            }
            else if (SharedDump.Utils.UrlParsing.IsHackerNewsUrl(url))
            {
                _logger.LogInformation("Detected Hacker News URL");
                serviceType = "hackernews";
                var hackerNewsId = SharedDump.Utils.UrlParsing.ExtractHackerNewsId(url);
                if (string.IsNullOrEmpty(hackerNewsId))
                {
                    _logger.LogWarning("Could not extract Hacker News ID from URL: {Url}", url);
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Could not extract Hacker News ID from URL");
                    return badResponse;
                }
                platformData = await GetHackerNewsDataForAnalysis(hackerNewsId, maxComments);
                commentsText = ConvertHackerNewsDataToCommentsText(platformData);
            }
            else
            {
                _logger.LogWarning("Unsupported URL format: {Url}", url);
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Unsupported URL format. Supported platforms: GitHub, YouTube, Reddit, DevBlogs, Twitter/X, BlueSky, Hacker News");
                return response;
            }

            _logger.LogInformation("Platform data retrieved successfully for {ServiceType}", serviceType);

            // Perform analysis using the AnalyzeComments logic
            if (string.IsNullOrEmpty(commentsText))
            {
                _logger.LogInformation("No comments available for analysis");
                var noCommentsResponse = req.CreateResponse(HttpStatusCode.OK);
                await noCommentsResponse.WriteStringAsync("## No Comments Available\n\nThere are no comments to analyze at this time.");
                return noCommentsResponse;
            }

            _logger.LogInformation("Starting analysis for {ServiceType} with comments length: {Length}", serviceType, commentsText.Length);

            var analysisBuilder = new System.Text.StringBuilder();
            await foreach (var update in _analyzerService.GetStreamingAnalysisAsync(
                serviceType, 
                commentsText,
                customPrompt))
            {
                analysisBuilder.Append(update);
            }

            _logger.LogInformation("Analysis completed successfully, response length: {Length}", analysisBuilder.Length);

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteStringAsync(analysisBuilder.ToString());
            
            // Track API usage on successful completion (AutoAnalyze = 1 usage point)
            await ApiKeyValidationHelper.TrackApiUsageAsync(userId!, 1, _userAccountService, _logger, url);
            
            _logger.LogInformation("Auto-analyze request completed successfully");
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-analyze request");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("An error occurred processing the request");
            return response;
        }
    }

    #region Helper Methods for AutoAnalyze

    private async Task<object> GetGitHubDataForAnalysis(string url, int maxComments)
    {
        var urlInfo = SharedDump.Utils.GitHubUrlParser.ParseGitHubUrl(url);
        if (urlInfo == null)
            throw new InvalidOperationException("Invalid GitHub URL provided");

        if (!await _githubService.CheckRepositoryValid(urlInfo.Owner, urlInfo.Repository))
            throw new InvalidOperationException("Invalid repository");

        object? result = urlInfo.Type switch
        {
            SharedDump.Utils.GitHubUrlType.Issue when urlInfo.Number.HasValue => 
                await _githubService.GetIssueWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value),
            SharedDump.Utils.GitHubUrlType.PullRequest when urlInfo.Number.HasValue => 
                await _githubService.GetPullRequestWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value),
            SharedDump.Utils.GitHubUrlType.Discussion when urlInfo.Number.HasValue => 
                await _githubService.GetDiscussionWithCommentsAsync(urlInfo.Owner, urlInfo.Repository, urlInfo.Number.Value),
            _ => throw new InvalidOperationException("URL must be a specific GitHub issue, pull request, or discussion")
        };

        if (result == null)
            throw new InvalidOperationException("GitHub content not found");

        return result;
    }

    private async Task<object> GetYouTubeDataForAnalysis(string url, int maxComments)
    {
        var videoId = SharedDump.Utils.UrlParsing.ExtractVideoId(url);
        if (string.IsNullOrEmpty(videoId))
            throw new InvalidOperationException("Could not extract YouTube video ID from URL");

        var video = await _ytService.ProcessVideo(videoId);
        if (video == null)
            throw new InvalidOperationException("No YouTube video found");

        // Limit comments to maxComments
        if (video.Comments.Count > maxComments)
        {
            video.Comments = video.Comments.Take(maxComments).ToList();
        }

        return new List<YouTubeOutputVideo> { video };
    }

    private async Task<object> GetRedditDataForAnalysis(string url, int maxComments)
    {
        var threadId = SharedDump.Utils.UrlParsing.ExtractRedditId(url);
        if (string.IsNullOrEmpty(threadId))
            throw new InvalidOperationException("Could not extract Reddit thread ID from URL");

        if (SharedDump.Utils.RedditUrlParser.IsRedditShortUrl(threadId))
        {
            var resolvedId = await SharedDump.Utils.RedditUrlParser.GetShortlinkIdAsync(threadId);
            if (string.IsNullOrWhiteSpace(resolvedId))
                throw new InvalidOperationException("Could not resolve Reddit short URL");
            
            threadId = resolvedId;
        }

        var thread = await _redditService.GetThreadWithComments(threadId);
        if (thread == null)
            throw new InvalidOperationException("No Reddit thread found");

        return thread;
    }

    private async Task<object> GetDevBlogsDataForAnalysis(string url, int maxComments)
    {
        var article = await _devBlogsService.FetchArticleWithCommentsAsync(url);
        if (article == null)
            throw new InvalidOperationException("Could not fetch DevBlogs article or comments");

        return article;
    }

    private async Task<object> GetTwitterDataForAnalysis(string url, int maxComments)
    {
        var result = await _twitterService.GetTwitterThreadAsync(url);
        if (result == null)
            throw new InvalidOperationException("Could not fetch Twitter/X feedback");

        return result;
    }

    private async Task<object> GetBlueSkyDataForAnalysis(string url, int maxComments)
    {
        var result = await _blueSkyService.GetBlueSkyPostAsync(url);
        if (result == null)
            throw new InvalidOperationException("Could not fetch BlueSky feedback");

        return result;
    }

    private async Task<object> GetHackerNewsDataForAnalysis(string storyId, int maxComments)
    {
        if (!int.TryParse(storyId, out var id))
            throw new InvalidOperationException("Invalid Hacker News story ID");

        var items = _hnService.GetItemWithComments(id);
        var itemsList = new List<HackerNewsItem>();
        await foreach (var item in items)
        {
            itemsList.Add(item);
        }
        
        if (!itemsList.Any())
            throw new InvalidOperationException("No Hacker News story found");

        return itemsList;
    }

    private string ConvertGitHubDataToCommentsText(object data)
    {
        return data switch
        {
            GithubIssueModel issue => ConvertGitHubIssuesToCommentsText(new[] { issue }),
            GithubDiscussionModel discussion => ConvertGitHubDiscussionsToCommentsText(new[] { discussion }),
            _ => JsonSerializer.Serialize(data)
        };
    }

    private string ConvertGitHubIssuesToCommentsText(GithubIssueModel[] issues)
    {
        var threads = SharedDump.Services.CommentDataConverter.ConvertGitHubIssues(issues.ToList());
        return ConvertCommentThreadsToText(threads);
    }

    private string ConvertGitHubDiscussionsToCommentsText(GithubDiscussionModel[] discussions)
    {
        var threads = SharedDump.Services.CommentDataConverter.ConvertGitHubDiscussions(discussions.ToList());
        return ConvertCommentThreadsToText(threads);
    }

    private string ConvertYouTubeDataToCommentsText(object data)
    {
        if (data is List<YouTubeOutputVideo> videos)
        {
            var threads = SharedDump.Services.CommentDataConverter.ConvertYouTube(videos);
            return ConvertCommentThreadsToText(threads);
        }
        return JsonSerializer.Serialize(data);
    }

    private string ConvertRedditDataToCommentsText(object data)
    {
        if (data is RedditThreadModel thread)
        {
            var threads = SharedDump.Services.CommentDataConverter.ConvertReddit(new List<RedditThreadModel> { thread });
            return ConvertCommentThreadsToText(threads);
        }
        return JsonSerializer.Serialize(data);
    }

    private string ConvertDevBlogsDataToCommentsText(object data)
    {
        if (data is DevBlogsArticleModel article)
        {
            var threads = SharedDump.Services.CommentDataConverter.ConvertDevBlogs(article);
            return ConvertCommentThreadsToText(threads);
        }
        return JsonSerializer.Serialize(data);
    }

    private string ConvertTwitterDataToCommentsText(object data)
    {
        // For Twitter, we'll serialize the data since it has a specific structure expected by the analyzer
        return JsonSerializer.Serialize(data);
    }

    private string ConvertBlueSkyDataToCommentsText(object data)
    {
        if (data is BlueSkyFeedbackResponse response)
        {
            var threads = SharedDump.Services.CommentDataConverter.ConvertBlueSky(response);
            return ConvertCommentThreadsToText(threads);
        }
        return JsonSerializer.Serialize(data);
    }

    private string ConvertHackerNewsDataToCommentsText(object data)
    {
        if (data is List<HackerNewsItem> items)
        {
            var threads = SharedDump.Services.CommentDataConverter.ConvertHackerNews(items);
            return ConvertCommentThreadsToText(threads);
        }
        return JsonSerializer.Serialize(data);
    }

    private string ConvertCommentThreadsToText(List<CommentThread> threads)
    {
        if (!threads.Any())
            return string.Empty;

        var result = new System.Text.StringBuilder();

        foreach (var thread in threads)
        {
            result.AppendLine($"# {thread.Title}");
            if (!string.IsNullOrEmpty(thread.Description))
            {
                result.AppendLine($"Description: {thread.Description}");
            }
            result.AppendLine($"Author: {thread.Author}");
            result.AppendLine($"Created: {thread.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            result.AppendLine($"Source: {thread.SourceType}");
            if (!string.IsNullOrEmpty(thread.Url))
            {
                result.AppendLine($"URL: {thread.Url}");
            }
            result.AppendLine();

            if (thread.Comments.Any())
            {
                result.AppendLine("## Comments");
                AppendCommentsToText(result, thread.Comments, 0);
            }

            result.AppendLine("---");
        }

        return result.ToString();
    }

    private void AppendCommentsToText(System.Text.StringBuilder result, List<CommentData> comments, int depth)
    {
        foreach (var comment in comments)
        {
            var indent = new string(' ', depth * 2);
            result.AppendLine($"{indent}**{comment.Author}** ({comment.CreatedAt:yyyy-MM-dd HH:mm:ss}):");
            result.AppendLine($"{indent}{comment.Content}");
            if (comment.Score.HasValue)
            {
                result.AppendLine($"{indent}Score: {comment.Score}");
            }
            result.AppendLine();

            if (comment.Replies.Any())
            {
                AppendCommentsToText(result, comment.Replies, depth + 1);
            }
        }
    }

    #endregion
}
