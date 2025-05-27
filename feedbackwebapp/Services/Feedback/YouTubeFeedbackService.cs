using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.YouTube;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public class YouTubeFeedbackService : FeedbackService, IYouTubeFeedbackService
{
    private readonly string _videoIds;
    private readonly string _playlistIds;

    public YouTubeFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        string videoIds,
        string playlistIds,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _videoIds = videoIds;
        _playlistIds = playlistIds;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        var processedVideoIds = UrlParsing.ExtractVideoId(_videoIds);
        var processedPlaylistIds = UrlParsing.ExtractPlaylistId(_playlistIds);

        if (string.IsNullOrWhiteSpace(processedVideoIds) && string.IsNullOrWhiteSpace(processedPlaylistIds))
        {
            throw new InvalidOperationException("Please enter at least one valid video URL/ID or playlist URL/ID");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching YouTube comments...");

        var youTubeCode = Configuration["FeedbackApi:GetYouTubeFeedbackCode"]
            ?? throw new InvalidOperationException("YouTube API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the YouTube API
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(youTubeCode)}",
            $"maxComments={maxComments}"
        };
        
        if (!string.IsNullOrWhiteSpace(processedVideoIds))
        {
            queryParams.Add($"videos={Uri.EscapeDataString(processedVideoIds)}");
        }
        if (!string.IsNullOrWhiteSpace(processedPlaylistIds))
        {
            queryParams.Add($"playlists={Uri.EscapeDataString(processedPlaylistIds)}");
        }

        var getFeedbackUrl = $"{BaseUrl}/api/GetYouTubeFeedback?{string.Join("&", queryParams)}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
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
        
        // Build our comments string
        var allComments = string.Join("\n\n", videos.SelectMany(v => 
            v.Comments.Select(c => $"Video: {v.Title}\nComment by {c.Author}: {c.Text}")));

        return (allComments, totalComments, videos);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }


        // Analyze all commetns from the video
        int totalComments = commentCount ?? comments.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        var markdownResult = await AnalyzeCommentsInternal("YouTube", comments, totalComments);

        // Get the videos from additionalData and if we just have 1 then just return that one.
        var videos = additionalData as List<YouTubeOutputVideo>;
        if (videos is null || !videos.Any() || videos.Count == 1)
        {
            return (markdownResult, additionalData);
        }

        var markdownResults = new List<string>();
        
        markdownResults.Add(markdownResult);
        
        // Analyze each video separately if they have comments
        foreach (var video in videos)
        {
            if (video.Comments.Count == 0)
                continue;

            // Build comments string for this video
            var videoComments = $"Video: {video.Title}\n\n" + string.Join("\n\n", video.Comments.Select(c => $"Comment by {c.Author}: {c.Text}"));

            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {video.Comments.Count} comments for video: {video.Title}...");
            await Task.Delay(500); // Simulate some processing delay
            var videoMarkdown = await AnalyzeCommentsInternal($"YouTube", videoComments, video.Comments.Count);
            markdownResults.Add(videoMarkdown);
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