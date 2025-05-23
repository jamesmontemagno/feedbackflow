using System.ComponentModel;
using ModelContextProtocol.Server;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.TwitterFeedback;
using Microsoft.Extensions.Logging;

namespace FeedbackMCP;

/// <summary>
/// Model Context Protocol server tool for FeedbackFlow
/// </summary>
/// <remarks>
/// This tool provides MCP capabilities for fetching and analyzing feedback 
/// from various sources like GitHub, Hacker News, YouTube, Reddit, and Twitter.
/// It's designed to be used with the ModelContextProtocol server framework.
/// </remarks>
[McpServerToolType]
public class FeedbackFlowTool
{
    private readonly ApiConfiguration _configuration;
    private readonly GitHubService _githubService;
    private readonly HackerNewsService _hnService;
    private readonly YouTubeService _ytService;
    private readonly RedditService _redditService;
    private readonly HttpClient _httpClient;
    private readonly TwitterFeedbackFetcher _twitterService;

    /// <summary>
    /// Initializes a new instance of the FeedbackFlowTool class
    /// </summary>
    /// <param name="configuration">API configuration containing keys and tokens</param>
    /// <param name="httpClient">HTTP client for making API requests</param>
    /// <param name="twitterLogger">Logger for Twitter API operations</param>
    public FeedbackFlowTool(ApiConfiguration configuration, HttpClient httpClient, ILogger<TwitterFeedbackFetcher> twitterLogger)
    {
        _configuration = configuration;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _githubService = new GitHubService(configuration.GitHubAccessToken, _httpClient);
        _hnService = new HackerNewsService(_httpClient);
        _ytService = new YouTubeService(configuration.YouTubeApiKey, _httpClient);
        _redditService = new RedditService(configuration.RedditClientId, configuration.RedditClientSecret, _httpClient);
        _twitterService = new TwitterFeedbackFetcher(_httpClient, twitterLogger ?? throw new ArgumentNullException(nameof(twitterLogger)));
    }

    [McpServerTool(Name = "github-comments")]
    /// <summary>
    /// Gets comments from a GitHub issue
    /// </summary>
    /// <param name="owner">The owner of the repository</param>
    /// <param name="repo">The name of the repository</param>
    /// <param name="issueNumber">The issue number to get comments from</param>
    /// <returns>GitHub issue with comments</returns>
    /// <remarks>
    /// This method validates the repository existence before fetching comments.
    /// It returns the issue details along with all its comments.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the repository does not exist or is not accessible</exception>
    /// <example>
    /// <code>
    /// // Example MCP client usage
    /// var githubComments = await feedbackTool.GetGitHubComments("dotnet", "maui", "123");
    /// </code>
    /// </example>
    [Description("Get comments from a GitHub issue")]
    public async Task<object> GetGitHubComments(
        [Description("The owner of the repository")] string owner,
        [Description("The name of the repository")] string repo,
        [Description("The issue number to get comments from")] string issueNumber)
    {
        if (!await _githubService.CheckRepositoryValid(owner, repo))
        {
            throw new ArgumentException($"Repository {owner}/{repo} not found or not accessible");
        }

        var issues = await _githubService.GetIssuesAsync(owner, repo, Array.Empty<string>());
        var issue = issues.Find(i => i.Id == issueNumber);
        
        if (issue == null)
        {
            throw new ArgumentException($"Issue {issueNumber} not found in repository {owner}/{repo}");
        }

        return new { title = issue.Title, comments = issue.Comments };
    }

    /// <summary>
    /// Gets comments from a Hacker News story
    /// </summary>
    /// <param name="storyId">The ID of the Hacker News story</param>
    /// <returns>Collection of comments from the Hacker News story</returns>
    /// <remarks>
    /// This method retrieves all comments associated with a specific Hacker News story.
    /// It validates that the item is actually a story before retrieving its comments.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the story doesn't exist or isn't a story type</exception>
    /// <example>
    /// <code>
    /// var comments = await feedbackTool.GetHackerNewsComments(12345);
    /// </code>
    /// </example>
    [McpServerTool(Name = "hackernews-comments")]
    [Description("Get comments from a Hacker News story")]
    public async Task<IEnumerable<object>> GetHackerNewsComments(
        [Description("The ID of the Hacker News story")] int storyId)
    {
        var story = await _hnService.GetItemData(storyId);
        if (story?.Type != "story")
        {
            throw new ArgumentException($"Item {storyId} is not a valid HackerNews story");
        }

        var comments = new List<object>();
        await foreach (var comment in _hnService.GetItemWithComments(storyId))
        {
            comments.Add(new
            {
                id = comment.Id,
                author = comment.By,
                text = comment.Text,
                time = DateTimeOffset.FromUnixTimeSeconds(comment.Time).DateTime,
                parent = comment.Parent
            });
        }

        return comments;
    }

    /// <summary>
    /// Gets comments from a YouTube video
    /// </summary>
    /// <param name="videoId">The ID of the YouTube video</param>
    /// <returns>List containing the video with its comments</returns>
    /// <remarks>
    /// This method fetches a specific YouTube video by ID and retrieves all of its comments.
    /// The result includes video metadata and comments with author information.
    /// </remarks>
    /// <example>
    /// <code>
    /// var videoWithComments = await feedbackTool.GetYouTubeComments("dQw4w9WgXcQ");
    /// </code>
    /// </example>
    [McpServerTool(Name = "youtube-video-comments")]
    [Description("Get comments from a YouTube video")]
    public async Task<IReadOnlyList<YouTubeOutputVideo>> GetYouTubeComments(
        [Description("The ID of the YouTube video")] string videoId)
    {
        var videos = new List<YouTubeOutputVideo>();
        var video = await _ytService.ProcessVideo(videoId);
        videos.Add(video);
        return videos;
    }

    /// <summary>
    /// Gets comments from all videos in a YouTube channel
    /// </summary>
    /// <param name="channelId">The ID of the YouTube channel</param>
    /// <returns>List of videos with their comments from the channel</returns>
    /// <remarks>
    /// This method fetches all videos from a YouTube channel and retrieves their comments.
    /// The result includes video metadata and comments for each video in the channel.
    /// Note that this may return a large amount of data for channels with many videos.
    /// </remarks>
    /// <example>
    /// <code>
    /// var channelVideos = await feedbackTool.GetYouTubeChannelComments("UCmXmlB4-HJytD7wek0Uo97A");
    /// </code>
    /// </example>
    [McpServerTool(Name = "youtube-channel-comments")]
    [Description("Get comments from all videos in a YouTube channel")]
    public async Task<IReadOnlyList<YouTubeOutputVideo>> GetYouTubeChannelComments(
        [Description("The ID of the YouTube channel")] string channelId)
    {
        var videos = new List<YouTubeOutputVideo>();
        var videoIds = await _ytService.GetChannelVideos(channelId);
        
        foreach (var videoId in videoIds)
        {
            var video = await _ytService.ProcessVideo(videoId);
            videos.Add(video);
        }

        return videos;
    }

    /// <summary>
    /// Gets comments from all videos in a YouTube playlist
    /// </summary>
    /// <param name="playlistId">The ID of the YouTube playlist</param>
    /// <returns>List of videos with their comments from the playlist</returns>
    /// <remarks>
    /// This method fetches all videos from a YouTube playlist and retrieves their comments.
    /// The result includes video metadata and comments for each video in the playlist.
    /// </remarks>
    /// <example>
    /// <code>
    /// var playlistVideos = await feedbackTool.GetYouTubePlaylistComments("PLlrxD0HtieHgHAF3uw8bT7ArEC5UmA4OY");
    /// </code>
    /// </example>
    [McpServerTool(Name = "youtube-playlist-comments")]
    [Description("Get comments from all videos in a YouTube playlist")]
    public async Task<IReadOnlyList<YouTubeOutputVideo>> GetYouTubePlaylistComments(
        [Description("The ID of the YouTube playlist")] string playlistId)
    {
        var videos = new List<YouTubeOutputVideo>();
        var videoIds = await _ytService.GetPlaylistVideos(playlistId);
        
        foreach (var videoId in videoIds)
        {
            var video = await _ytService.ProcessVideo(videoId);
            videos.Add(video);
        }

        return videos;
    }

    /// <summary>
    /// Gets comments from a Reddit thread
    /// </summary>
    /// <param name="threadId">The ID of the Reddit thread</param>
    /// <returns>Reddit thread model with all comments</returns>
    /// <remarks>
    /// This method fetches a Reddit thread by ID and retrieves all of its comments.
    /// The result includes thread metadata (title, author, score) and a nested 
    /// comment structure with comment text and author information.
    /// </remarks>
    /// <example>
    /// <code>
    /// var redditThread = await feedbackTool.GetRedditComments("t3_11xd3kp");
    /// </code>
    /// </example>
    [McpServerTool(Name = "reddit-comments")]
    [Description("Get comments from a Reddit thread")]
    public async Task<RedditThreadModel> GetRedditComments(
        [Description("The ID of the Reddit thread")] string threadId)
    {
        return await _redditService.GetThreadWithComments(threadId);
    }

    /// <summary>
    /// Get feedback (tweet and replies) from a Twitter/X post.
    /// Usage: Pass a tweet URL or ID. Returns the main tweet and its replies.
    /// <summary>
    /// Gets feedback (tweet and replies) from a Twitter/X post
    /// </summary>
    /// <param name="tweetUrlOrId">The tweet URL or ID</param>
    /// <returns>Twitter feedback response with the tweet and its replies</returns>
    /// <remarks>
    /// This method fetches a tweet by URL or ID and retrieves all of its replies.
    /// The result includes the original tweet content and metadata along with
    /// all replies in a threaded structure.
    /// </remarks>
    /// <example>
    /// Using tweet ID:
    /// <code>
    /// var twitterFeedback = await feedbackTool.GetTwitterFeedback("1580661436183433217");
    /// </code>
    /// 
    /// Using tweet URL:
    /// <code>
    /// var twitterFeedback = await feedbackTool.GetTwitterFeedback("https://twitter.com/user/status/1580661436183433217");
    /// </code>
    /// </example>
    [McpServerTool(Name = "twitter-feedback")]
    [Description("Get feedback (tweet and replies) from a Twitter/X post")]
    public async Task<TwitterFeedbackResponse?> GetTwitterFeedback(
        [Description("The tweet URL or ID")] string tweetUrlOrId)
    {
        return await _twitterService.FetchFeedbackAsync(tweetUrlOrId);
    }
}
