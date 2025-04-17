using System;
using System.ComponentModel;
using ModelContextProtocol.Server;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;

namespace FeedbackMCP;

[McpServerToolType]
public class FeedbackFlowTool
{
    private readonly ApiConfiguration _configuration;
    private readonly GitHubService _githubService;
    private readonly HackerNewsService _hnService;
    private readonly YouTubeService _ytService;
    private readonly RedditService _redditService;

    public FeedbackFlowTool(ApiConfiguration configuration)
    {
        _configuration = configuration;
        _githubService = new GitHubService(configuration.GitHubAccessToken);
        _hnService = new HackerNewsService();
        _ytService = new YouTubeService(configuration.YouTubeApiKey);
        _redditService = new RedditService(configuration.RedditClientId, configuration.RedditClientSecret);
    }

    [McpServerTool(Name = "github-comments")]
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

    [McpServerTool(Name = "reddit-comments")]
    [Description("Get comments from a Reddit thread")]
    public async Task<RedditThreadModel> GetRedditComments(
        [Description("The ID of the Reddit thread")] string threadId)
    {
        return await _redditService.GetThreadWithComments(threadId);
    }
}
