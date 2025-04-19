using System.Net.Http.Json;
using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Components.ContentFeed.Services;

public class YouTubeContentFeedService : ContentFeedService, IYouTubeContentFeedService
{
    private readonly string _topic;
    private readonly int _days;
    private readonly string? _tag;

    public YouTubeContentFeedService(
        string topic,
        int days,
        string? tag,
        HttpClient http,
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
        var query = $"{BaseUrl}/api/GetRecentYouTubeVideos?topic={Uri.EscapeDataString(_topic)}&days={_days}";
        if (!string.IsNullOrEmpty(_tag))
        {
            query += $"&tag={Uri.EscapeDataString(_tag)}";
        }
        
        return await Http.GetFromJsonAsync<List<YouTubeOutputVideo>>(query) 
            ?? new List<YouTubeOutputVideo>();
    }
}