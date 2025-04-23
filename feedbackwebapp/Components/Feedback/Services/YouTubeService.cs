using SharedDump.Models.YouTube;
using SharedDump.Utils;
using System.Text.Json;
using System.Text;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public class YouTubeService : FeedbackService, IYouTubeFeedbackService
{
    private readonly string _videoIds;
    private readonly string _playlistIds;

    public YouTubeService(
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

        // Get comments from the YouTube API
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(youTubeCode)}"
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
        
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Processing video data...");
        
        // Parse the YouTube response
        var videos = JsonSerializer.Deserialize<List<YouTubeOutputVideo>>(responseContent);
        
        if (videos == null || !videos.Any())
        {
            throw new InvalidOperationException("No comments found for the specified videos/playlists");
        }

        // Analyze the comments
        var markdownResult = await AnalyzeComments("YouTube", responseContent);

        return (markdownResult, videos);
    }
}