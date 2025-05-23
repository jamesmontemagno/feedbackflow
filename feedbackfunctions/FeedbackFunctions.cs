using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.DevBlogs;
using SharedDump.AI;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Json;

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
    private readonly IConfiguration _configuration;
    private readonly GitHubService _githubService;
    private readonly HackerNewsService _hnService;
    private readonly YouTubeService _ytService;
    private readonly RedditService _redditService;
    private readonly DevBlogsService _devBlogsService = new();
    private readonly IFeedbackAnalyzerService _analyzerService;
    private readonly TwitterFeedbackFetcher _twitterService;
    private readonly BlueSkyFeedbackFetcher _blueSkyService;

    /// <summary>
    /// Initializes a new instance of the FeedbackFunctions class
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="httpClientFactory">HTTP client factory for creating named clients</param>
    public FeedbackFunctions(ILogger<FeedbackFunctions> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
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

        
        var githubToken = _configuration["GitHub:AccessToken"];
        _githubService = new GitHubService(githubToken ?? throw new InvalidOperationException("GitHub access token not configured"), httpClientFactory.CreateClient("GitHub"));
        
        _hnService = new HackerNewsService(httpClientFactory.CreateClient("HackerNews"));
        
        var ytApiKey = _configuration["YouTube:ApiKey"];
        _ytService = new YouTubeService(ytApiKey ?? throw new InvalidOperationException("YouTube API key not configured"), httpClientFactory.CreateClient("YouTube"));

        var redditClientId = _configuration["Reddit:ClientId"];
        var redditClientSecret = _configuration["Reddit:ClientSecret"];
        _redditService = new RedditService(
            redditClientId ?? throw new InvalidOperationException("Reddit client ID not configured"),
            redditClientSecret ?? throw new InvalidOperationException("Reddit client secret not configured"),
            httpClientFactory.CreateClient("Reddit"));

        _devBlogsService = new DevBlogsService(httpClientFactory.CreateClient("DevBlogs"), _logger);

        var endpoint = _configuration["Azure:OpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = _configuration["Azure:OpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key not configured");
        var deployment = _configuration["Azure:OpenAI:Deployment"] ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured");

        _analyzerService = new FeedbackAnalyzerService(endpoint, apiKey, deployment);

        var twitterBearerToken = _configuration["Twitter:BearerToken"] ?? throw new InvalidOperationException("Twitter Bearer token not configured");
        var twitterHttpClient = httpClientFactory.CreateClient("Twitter");
        twitterHttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", twitterBearerToken);
        _twitterService = new TwitterFeedbackFetcher(twitterHttpClient, Microsoft.Extensions.Logging.Abstractions.NullLogger<TwitterFeedbackFetcher>.Instance);
        
        // Initialize BlueSky service with authentication
        var blueSkyUsername = _configuration["BlueSky:Username"] ?? throw new InvalidOperationException("BlueSky username not configured");
        var blueSkyAppPassword = _configuration["BlueSky:AppPassword"] ?? throw new InvalidOperationException("BlueSky app password not configured");
        _blueSkyService = new BlueSkyFeedbackFetcher(httpClientFactory.CreateClient("BlueSky"), Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueSkyFeedbackFetcher>.Instance);
        _blueSkyService.SetCredentials(blueSkyUsername, blueSkyAppPassword);
    }

    /// <summary>
    /// Retrieves feedback from GitHub repositories (issues, pull requests, discussions)
    /// </summary>
    /// <param name="req">HTTP request containing query parameters</param>
    /// <returns>HTTP response with GitHub feedback data</returns>
    /// <remarks>
    /// Query parameters:
    /// - repo: Required. Repository in format "owner/name"
    /// - labels: Optional. Comma-separated list of issue/PR labels to filter by
    /// - issues: Optional. Boolean (default: true) whether to include issues
    /// - pulls: Optional. Boolean (default: false) whether to include pull requests
    /// - discussions: Optional. Boolean (default: false) whether to include discussions
    /// 
    /// Example usage:
    /// GET /api/GetGitHubFeedback?repo=dotnet/maui&amp;labels=bug,documentation&amp;pulls=true
    /// </remarks>
    [Function("GetGitHubFeedback")]
    public async Task<HttpResponseData> GetGitHubFeedback(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GitHub feedback request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var repo = queryParams["repo"];
        var labels = queryParams["labels"]?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var includeIssues = bool.Parse(queryParams["issues"] ?? "true");
        var includePullRequests = bool.Parse(queryParams["pulls"] ?? "false");
        var includeDiscussions = bool.Parse(queryParams["discussions"] ?? "false");

        if (string.IsNullOrEmpty(repo))
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("'repo' parameter is required");
            return response;
        }

        try
        {
            var (repoOwner, repoName) = repo.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
            {
                [var owner, var name] => (owner, name),
                _ => throw new InvalidOperationException("Invalid repository format, expected owner/repo")
            };

            if (!await _githubService.CheckRepositoryValid(repoOwner, repoName))
            {
                var response2 = req.CreateResponse(HttpStatusCode.BadRequest);
                await response2.WriteStringAsync("Invalid repository");
                return response2;
            }

            var discussionsTask = includeDiscussions ? _githubService.GetDiscussionsAsync(repoOwner, repoName) : Task.FromResult(new List<GithubDiscussionModel>());
            var issuesTask = includeIssues ? _githubService.GetIssuesAsync(repoOwner, repoName, labels) : Task.FromResult(new List<GithubIssueModel>());
            var pullsTask = includePullRequests ? _githubService.GetPullRequestsAsync(repoOwner, repoName, labels) : Task.FromResult(new List<GithubIssueModel>());

            await Task.WhenAll(discussionsTask, issuesTask, pullsTask);

            var result = new
            {
                Discussions = await discussionsTask,
                Issues = await issuesTask,
                PullRequests = await pullsTask
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub feedback request");
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

    [Function("GetGitHubItemComments")]
    public async Task<HttpResponseData> GetGitHubItemComments(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing GitHub item comments request");

        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var repo = queryParams["repo"];
        var itemType = queryParams["type"]?.ToLowerInvariant() ?? "issue";
        
        if (!int.TryParse(queryParams["number"], out var itemNumber))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("'number' parameter is required and must be an integer");
            return badResponse;
        }

        if (string.IsNullOrEmpty(repo))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("'repo' parameter is required");
            return badResponse;
        }

        try
        {
            var (repoOwner, repoName) = repo.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
            {
                [var owner, var name] => (owner, name),
                _ => throw new InvalidOperationException("Invalid repository format, expected owner/repo")
            };

            if (!await _githubService.CheckRepositoryValid(repoOwner, repoName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid repository");
                return errorResponse;
            }

            List<GithubCommentModel> comments = itemType switch
            {
                "issue" => await _githubService.GetIssueCommentsAsync(repoOwner, repoName, itemNumber),
                "pull" or "pr" => await _githubService.GetPullRequestCommentsAsync(repoOwner, repoName, itemNumber),
                "discussion" => await _githubService.GetDiscussionCommentsAsync(repoOwner, repoName, itemNumber),
                _ => throw new ArgumentException($"Invalid item type: {itemType}. Expected 'issue', 'pull', or 'discussion'")
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(comments);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GitHub item comments request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred processing the request");
            return errorResponse;
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
            var result = await _twitterService.FetchFeedbackAsync(tweetUrlOrId);
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
            var result = await _blueSkyService.FetchFeedbackAsync(postUrlOrId);
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
