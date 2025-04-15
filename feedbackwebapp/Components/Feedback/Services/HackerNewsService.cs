using SharedDump.Utils;
using System.Text.Json;
using System.Text;

namespace FeedbackWebApp.Components.Feedback.Services;

public class HackerNewsService : FeedbackService, IHackerNewsFeedbackService
{
    private readonly string _itemId;

    public HackerNewsService(
        HttpClient http, 
        IConfiguration configuration, 
        string itemId,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, onStatusUpdate)
    {
        _itemId = itemId;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Hacker News comments...");

        var code = Configuration["FeedbackApi:HackerNewsCommentsCode"]
            ?? throw new InvalidOperationException("HackerNews API code not configured");

        var requestBody = JsonSerializer.Serialize(new { itemId = _itemId });
        var content = new StringContent(
            requestBody, 
            Encoding.UTF8, 
            "application/json");

        var getCommentsUrl = $"{BaseUrl}/api/GetHackerNewsComments?code={Uri.EscapeDataString(code)}";
        var commentsResponse = await Http.PostAsync(getCommentsUrl, content);

        commentsResponse.EnsureSuccessStatusCode();
        var comments = await commentsResponse.Content.ReadAsStringAsync();

        var analysis = await AnalyzeComments("HackerNews", comments);

        return (analysis, null);
    }
}