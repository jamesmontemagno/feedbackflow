using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Services.ContentFeed;

public class YouTubeContentFeedService : ContentFeedService, IYouTubeContentFeedService
{
    private readonly string _topic;
    private readonly int _days;
    private readonly string? _tag;

    public YouTubeContentFeedService(
        string topic,
        int days,
        string? tag,
        IHttpClientFactory http,
        IConfiguration configuration)
        : base(http, configuration)
    {
        _topic = topic;
        _days = days;
        _tag = tag;
    }

    public override async Task<object?> FetchContent()
    {
        return await ((IYouTubeContentFeedService)this).FetchContent();
    }

    async Task<List<YouTubeOutputVideo>> IYouTubeContentFeedService.FetchContent()
    {
        var youtubeCode = Configuration["FeedbackApi:GetYouTubeContentFeedCode"] 
            ?? throw new InvalidOperationException("YouTube API code not configured");

        var query = $"{BaseUrl}/api/GetRecentYouTubeVideos?code={Uri.EscapeDataString(youtubeCode)}&topic={Uri.EscapeDataString(_topic)}&days={_days}";
        if (!string.IsNullOrEmpty(_tag))
        {
            query += $"&tag={Uri.EscapeDataString(_tag)}";
        }
        
        return await Http.GetFromJsonAsync<List<YouTubeOutputVideo>>(query) 
            ?? new List<YouTubeOutputVideo>();
    }
}