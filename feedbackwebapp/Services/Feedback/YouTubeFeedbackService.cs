using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.YouTube;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public class YouTubeFeedbackService : FeedbackService, IYouTubeFeedbackService
{
    private readonly string _videoId;
    private readonly string _playlistId;

    public YouTubeFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        string videoId,
        string playlistId,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
    {
        _videoId = videoId;
        _playlistId = playlistId;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null)
    {
        var processedVideoId = UrlParsing.ExtractVideoId(_videoId);
        var processedPlaylistId = UrlParsing.ExtractPlaylistId(_playlistId);

        if (string.IsNullOrWhiteSpace(processedVideoId) && string.IsNullOrWhiteSpace(processedPlaylistId))
        {
            throw new InvalidOperationException("Please enter a valid video URL/ID or playlist URL/ID");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching YouTube comments...");

        var youTubeCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("YouTube API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze(maxCommentsOverride);

        // Get comments from the YouTube API
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(youTubeCode)}",
            $"maxComments={maxComments}"
        };
        
        if (!string.IsNullOrWhiteSpace(processedVideoId))
        {
            queryParams.Add($"videos={Uri.EscapeDataString(processedVideoId)}");
        }
        if (!string.IsNullOrWhiteSpace(processedPlaylistId))
        {
            queryParams.Add($"playlists={Uri.EscapeDataString(processedPlaylistId)}");
        }

        var getFeedbackUrl = $"{BaseUrl}/api/GetYouTubeFeedback?{string.Join("&", queryParams)}";
        var feedbackResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Get, getFeedbackUrl);
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        
        // Parse the YouTube response
        var videos = JsonSerializer.Deserialize<List<YouTubeOutputVideo>>(responseContent);
        
        if (videos == null || !videos.Any())
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("No comments available", 0, null);
        }

        var totalComments = videos.Sum(v => v.Comments.Count);
        UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Found {totalComments} comments across {videos.Count} videos...");
        
   

        return (responseContent, totalComments, videos);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }


        // Analyze all comments from the video using optimized conversion
        int totalComments = commentCount ?? comments.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        var markdownResult = await AnalyzeCommentsWithOptimization("youtube", comments, totalComments, additionalData);

        // Get the videos from additionalData and if we just have 1 then just return that one.
        var videos = additionalData as List<YouTubeOutputVideo>;
        if (videos is null || !videos.Any() || videos.Count == 1)
        {
            return (markdownResult, additionalData);
        }

        var markdownResults = new List<string>
        {
            $"## YouTube Comments Analysis for Playlist\n",
            markdownResult
        };

        // Analyze each video separately if they have comments
        foreach (var video in videos)
        {
            if (video.Comments.Count == 0)
                continue;

            // Use optimized conversion for individual video analysis
            var singleVideoList = new List<YouTubeOutputVideo> { video };

            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {video.Comments.Count} comments for video: {video.Title}...");
            await Task.Delay(1500); // Simulate some processing delay
            try
            {
                var videoMarkdown = await AnalyzeCommentsWithOptimization("youtube", string.Empty, video.Comments.Count, singleVideoList);
                markdownResults.Add($"## YouTube Comments Analysis for : {video.Title}\n");
                markdownResults.Add(videoMarkdown);
            }
            catch (Exception ex)
            {
                UpdateStatus(FeedbackProcessStatus.Completed, $"Error analyzing video {video.Title}: {ex.Message}");
                markdownResults.Add($"## Error Analyzing Video: {video.Title}\n\n{ex.Message}");
            }
        }

        // Combine all results
        var combinedMarkdown = string.Join("\n\n---\n\n", markdownResults);
        
        return (combinedMarkdown, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments) || comments == "No comments available")
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}