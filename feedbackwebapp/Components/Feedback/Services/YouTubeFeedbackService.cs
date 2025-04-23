using SharedDump.Models.YouTube;
using SharedDump.Utils;
using System.Text.Json;

namespace FeedbackWebApp.Components.Feedback.Services;

public class YouTubeFeedbackService : FeedbackService, IYouTubeFeedbackService
{
    private readonly string _videoIds;
    private readonly string _playlistIds;

    public YouTubeFeedbackService(
        HttpClient http, 
        IConfiguration configuration,
        string videoIds,
        string playlistIds,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, onStatusUpdate)
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

        // Sort and limit comments for each video
        foreach (var video in videos)
        {
            if (video.Comments != null)
                video.Comments = video.Comments.OrderBy(c => c.PublishedAt).Take(MaxCommentsToAnalyze).ToList();
        }

        var limitedJson = JsonSerializer.Serialize(videos);
        var analysisBuilder = new System.Text.StringBuilder();

        // If this is a playlist (more than one video), do an overall analysis first
        if (videos.Count > 1)
        {
            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing overall playlist feedback...");
            var overallAnalysis = await AnalyzeComments("YouTube", limitedJson);
            analysisBuilder.AppendLine("# Overall Playlist Analysis");
            analysisBuilder.AppendLine();
            analysisBuilder.AppendLine(overallAnalysis);
            analysisBuilder.AppendLine();
            analysisBuilder.AppendLine("---");
            analysisBuilder.AppendLine();

            // Process individual videos with progress updates
            analysisBuilder.AppendLine("# Individual Video Analyses");
            analysisBuilder.AppendLine();

            for (int i = 0; i < videos.Count; i++)
            {
                var video = videos[i];
                UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing video {i + 1} of {videos.Count}: {video.Title}");
                
                var videoJson = JsonSerializer.Serialize(new List<YouTubeOutputVideo> { video });
                var videoAnalysis = await AnalyzeComments("YouTube", videoJson);
                
                analysisBuilder.AppendLine($"## {video.Title}");
                analysisBuilder.AppendLine($"Video URL: {video.Url}");
                analysisBuilder.AppendLine();
                analysisBuilder.AppendLine(videoAnalysis);
                analysisBuilder.AppendLine();
                analysisBuilder.AppendLine("---");
                analysisBuilder.AppendLine();
            }
        }
        // For single video, just use the original analysis
        else
        {
            var singleVideoAnalysis = await AnalyzeComments("YouTube", limitedJson);
            analysisBuilder.Append(singleVideoAnalysis);
        }

        return (analysisBuilder.ToString(), videos);
    }
}