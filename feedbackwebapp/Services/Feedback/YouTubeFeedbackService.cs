using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.YouTube;
using SharedDump.Utils;
using SharedDump.AI;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FeedbackWebApp.Services.Feedback;

public class YouTubeFeedbackService : FeedbackService, IYouTubeFeedbackService, IYouTubeContentTypeAware
{
    private readonly string _videoId;
    private readonly string _playlistId;
    private readonly ILogger<YouTubeFeedbackService> _logger;
    private YouTubeContentType _contentType = YouTubeContentType.Comments;

    public YouTubeFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        ILogger<YouTubeFeedbackService> logger,
        string videoId,
        string playlistId,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
    {
        _videoId = videoId;
        _playlistId = playlistId;
        _logger = logger;
    }

    public void SetContentType(YouTubeContentType contentType)
    {
        _contentType = contentType;
    }

    public YouTubeContentType GetContentType()
    {
        return _contentType;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
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

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the YouTube API
        var queryParams = new List<string>
        {
            $"code={Uri.EscapeDataString(youTubeCode)}",
            $"maxComments={maxComments}",
            $"contentType={_contentType}"
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
        
        _logger.LogInformation("YouTube API response received. Content type: {ContentType}, Response length: {Length}", _contentType, responseContent.Length);
        Console.WriteLine($"📡 YouTube API Response: {responseContent.Length} characters");
        Console.WriteLine($"📡 Content Type: {_contentType}");
        
        // Parse the YouTube response
        var videos = JsonSerializer.Deserialize<List<YouTubeOutputVideo>>(responseContent, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });
        
        if (videos == null || !videos.Any())
        {
            _logger.LogWarning("No videos returned from YouTube API");
            UpdateStatus(FeedbackProcessStatus.Completed, "No content to analyze");
            return ("No content available", 0, null);
        }

        _logger.LogInformation("Parsed {VideoCount} videos from YouTube API", videos.Count);
        
        // Log transcript information for each video
        foreach (var video in videos)
        {
            if (video.Transcript != null)
            {
                var transcriptLength = video.Transcript.FullText.Length;
                var segmentCount = video.Transcript.Segments.Count;
                _logger.LogInformation("Video '{Title}' has transcript: {Length} characters, {SegmentCount} segments", 
                    video.Title, transcriptLength, segmentCount);
                Console.WriteLine($"✅ GOT TRANSCRIPT for '{video.Title}': {transcriptLength} characters in {segmentCount} segments");
            }
            else
            {
                _logger.LogInformation("Video '{Title}' has NO transcript", video.Title);
                Console.WriteLine($"❌ NO TRANSCRIPT for '{video.Title}'");
            }
        }

        var totalComments = videos.Sum(v => v.Comments.Count);
        var totalTranscripts = videos.Count(v => v.Transcript != null);
        var totalTranscriptSegments = videos.Where(v => v.Transcript != null).Sum(v => v.Transcript!.Segments.Count);
        
        // Build status message based on what we're analyzing
        var statusParts = new List<string>();
        if (totalComments > 0)
            statusParts.Add($"{totalComments} comment{(totalComments != 1 ? "s" : "")}");
        if (totalTranscripts > 0)
            statusParts.Add($"{totalTranscripts} transcript{(totalTranscripts != 1 ? "s" : "")}");
        
        var statusMessage = statusParts.Any() 
            ? $"Found {string.Join(" and ", statusParts)} across {videos.Count} video{(videos.Count != 1 ? "s" : "")}..."
            : "Processing videos...";
            
        UpdateStatus(FeedbackProcessStatus.GatheringComments, statusMessage);
        
        // Format the content for analysis - convert to readable text with transcripts as timestamped segments
        var formattedContent = FormatYouTubeContent(videos);
        var totalItems = totalComments + totalTranscriptSegments;

        return (formattedContent, totalItems, videos);
    }

    private string FormatYouTubeContent(List<YouTubeOutputVideo> videos)
    {
        var contentBuilder = new System.Text.StringBuilder();
        var hasComments = videos.Any(v => v.Comments.Any());
        var hasTranscripts = videos.Any(v => v.Transcript != null);
        
        foreach (var video in videos)
        {
            contentBuilder.AppendLine($"=== Video: {video.Title} ===");
            contentBuilder.AppendLine($"URL: {video.Url}");
            contentBuilder.AppendLine();
            
            // Add comments if present
            if (video.Comments.Any())
            {
                contentBuilder.AppendLine("--- Comments ---");
                foreach (var comment in video.Comments)
                {
                    contentBuilder.AppendLine($"Comment by {comment.Author} at {comment.PublishedAt:yyyy-MM-dd HH:mm}:");
                    contentBuilder.AppendLine(comment.Text);
                    contentBuilder.AppendLine();
                }
            }
            
            // Add full transcript as one blob if present
            if (video.Transcript != null && video.Transcript.Segments.Any())
            {
                contentBuilder.AppendLine("--- Full Video Transcript ---");
                contentBuilder.AppendLine("This is the complete transcript of the video:");
                contentBuilder.AppendLine();
                
                // Combine all segments into one continuous text
                var fullTranscript = string.Join(" ", video.Transcript.Segments.Select(s => s.Text));
                contentBuilder.AppendLine(fullTranscript);
                contentBuilder.AppendLine();
            }
            
            contentBuilder.AppendLine();
        }
        
        return contentBuilder.ToString();
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Determine what we're analyzing based on the videos
        var videos = additionalData as List<YouTubeOutputVideo>;
        var hasComments = videos?.Any(v => v.Comments.Any()) ?? false;
        var hasTranscripts = videos?.Any(v => v.Transcript != null) ?? false;
        
        var totalCommentsCount = videos?.Sum(v => v.Comments.Count) ?? 0;
        var totalTranscriptsCount = videos?.Count(v => v.Transcript != null) ?? 0;
        
        // Build status message based on what we're analyzing
        var statusParts = new List<string>();
        if (totalCommentsCount > 0)
            statusParts.Add($"{totalCommentsCount} comment{(totalCommentsCount != 1 ? "s" : "")}");
        if (totalTranscriptsCount > 0)
            statusParts.Add($"{totalTranscriptsCount} transcript{(totalTranscriptsCount != 1 ? "s" : "")}");
        
        var analyzingMessage = statusParts.Any() 
            ? $"Analyzing {string.Join(" and ", statusParts)}. Estimated time: 10 seconds..."
            : "Analyzing content...";
            
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, analyzingMessage);
        
        // Build custom prompt with context based on content type
        string? customPromptWithContext = null;
        if (hasTranscripts || hasComments)
        {
            // For transcript-only analysis, use the specialized VideoTranscript prompt
            if (hasTranscripts && !hasComments)
            {
                customPromptWithContext = SharedDump.AI.FeedbackAnalyzerService.GetPromptByType(SharedDump.AI.PromptType.VideoTranscript);
            }
            else
            {
                // Get the base YouTube/Product feedback prompt
                var basePrompt = SharedDump.AI.FeedbackAnalyzerService.GetServiceSpecificPrompt("YouTube");
                
                // Add context at the beginning for mixed content (both comments and transcripts)
                var contextPrefix = "";
                if (hasTranscripts && hasComments)
                {
                    contextPrefix = "IMPORTANT CONTEXT: You are analyzing both user comments AND the full video transcript. Cross-reference the transcript content with viewer comments to identify alignment, disconnects, and content improvement opportunities.\n\n";
                }
                
                if (!string.IsNullOrEmpty(contextPrefix))
                {
                    customPromptWithContext = contextPrefix + basePrompt;
                }
            }
        }
        
        // Analyze all content - use base method WITHOUT status update since we already set it above
        int totalItems = commentCount ?? comments.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        var markdownResult = await AnalyzeCommentsInternalWithoutStatusUpdate("YouTube", comments, totalItems, customPromptWithContext);

        // Get the videos from additionalData and if we just have 1 then just return that one.
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

            // Build comments string for this video
            var videoComments = $"Video: {video.Title}\n\n" + string.Join("\n\n", video.Comments.Select(c => $"Comment by {c.Author}: {c.Text}"));

            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {video.Comments.Count} comments for video: {video.Title}...");
            await Task.Delay(1500); // Simulate some processing delay
            try
            {
                var videoMarkdown = await AnalyzeCommentsInternal($"YouTube", videoComments, video.Comments.Count);
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