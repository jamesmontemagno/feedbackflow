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
        HttpClient http, 
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

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
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
            throw new InvalidOperationException("No comments found for the specified videos/playlists");
        }

        var totalComments = videos.Sum(v => v.Comments.Count);
        UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Found {totalComments} comments across {videos.Count} videos...");        // Build our analysis request with all comments
        var allComments = string.Join("\n\n", videos.SelectMany(v => 
            v.Comments.Select(c => $"Video: {v.Title}\nComment by {c.Author}: {c.Text}")));

        // Analyze the comments
        var markdownResult = await AnalyzeComments("YouTube", allComments, totalComments);
        return (markdownResult, videos);
    }
}