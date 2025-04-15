using SharedDump.Models.YouTube;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Components.Feedback.Services;

public class YouTubeFeedbackService : FeedbackService
{
    private readonly string _videoIds;
    private readonly string _playlistIds;

    public YouTubeFeedbackService(
        HttpClient http, 
        IConfiguration configuration,
        string videoIds,
        string playlistIds) : base(http, configuration)
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