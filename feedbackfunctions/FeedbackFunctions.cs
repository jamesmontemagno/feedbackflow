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
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure;

namespace FeedbackFunctions
{
    public class FeedbackFunctions
    {
        private readonly ILogger<FeedbackFunctions> _logger;
        private readonly IConfiguration _configuration;
        private readonly GitHubService _githubService;
        private readonly HackerNewsService _hnService;
        private readonly YouTubeService _ytService;
        private readonly IChatClient _chatClient;

        public FeedbackFunctions(ILogger<FeedbackFunctions> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            var githubToken = _configuration["Github:AccessToken"];
            _githubService = new GitHubService(githubToken ?? throw new InvalidOperationException("GitHub access token not configured"));
            
            _hnService = new HackerNewsService();
            
            var ytApiKey = _configuration["YouTube:ApiKey"];
            _ytService = new YouTubeService(ytApiKey ?? throw new InvalidOperationException("YouTube API key not configured"));

            var endpoint = _configuration["Azure:OpenAI:Endpoint"];
            var deployment = _configuration["Azure:OpenAI:Deployment"];
            var apiKey = _configuration["Azure:OpenAI:ApiKey"];

            _chatClient = new AzureOpenAIClient(
                    new Uri(endpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured")), 
                    new AzureKeyCredential(apiKey ?? throw new InvalidOperationException("Azure OpenAI API key not configured")))
                .GetChatClient(deployment ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured"))
                .AsIChatClient();
        }

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

        private string GetServiceSpecificPrompt(string serviceType)
        {
            return serviceType.ToLowerInvariant() switch
            {
                "youtube" => @"You are an expert at analyzing YouTube comments. 
                    When analyzing comments, provide:
                    1. A summary of the overall sentiment and viewer engagement
                    2. Key topics, timestamps, or sections of the video mentioned frequently
                    3. Most positive feedback and what viewers particularly liked
                    4. Most constructive criticism or suggestions for improvement
                    5. Common questions or points of confusion from viewers
                    Format your response in markdown.",

                "github" => @"You are an expert at analyzing GitHub feedback. 
                    When analyzing comments, provide:
                    1. A summary of the overall sentiment and discussion quality
                    2. Key technical topics, features, or issues mentioned
                    3. Most insightful or helpful contributions
                    4. Most critical issues or pain points raised
                    5. Common feature requests or enhancement suggestions
                    Format your response in markdown.",

                "hackernews" => @"You are an expert at analyzing Hacker News comments. 
                    When analyzing comments, provide:
                    1. A summary of the overall discussion quality and technical depth
                    2. Key technical insights and alternative perspectives shared
                    3. Most insightful technical contributions or explanations
                    4. Most critical technical concerns or limitations pointed out
                    5. Interesting related technologies or approaches mentioned
                    Format your response in markdown.",

                _ => throw new ArgumentException($"Invalid service type: {serviceType}", nameof(serviceType))
            };
        }

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

                var servicePrompt = GetServiceSpecificPrompt(request.ServiceType);
                var prompt = $@"{servicePrompt}

                    Comments to analyze:
                    {request.Comments}";

                var analysisBuilder = new System.Text.StringBuilder();
                await foreach (var update in ChatClientExtensions.GetStreamingResponseAsync(_chatClient, prompt))
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
    }

    public class AnalyzeCommentsRequest
    {
        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;
        
        [JsonPropertyName("serviceType")]
        public string ServiceType { get; set; } = string.Empty;
    }
}
