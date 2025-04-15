using SharedDump.Utils;

namespace FeedbackWebApp.Components.Feedback.Services;

public class HackerNewsFeedbackService : FeedbackService, IHackerNewsFeedbackService
{
    private readonly string _storyIds;

    public HackerNewsFeedbackService(
        HttpClient http, 
        IConfiguration configuration,
        string storyIds) : base(http, configuration)
    {
        _storyIds = storyIds;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        var processedIds = UrlParsing.ExtractHackerNewsId(_storyIds);

        if (string.IsNullOrWhiteSpace(processedIds))
        {
            throw new InvalidOperationException("Please enter at least one valid Hacker News story ID or URL");
        }

        var hnCode = Configuration["FeedbackApi:GetHackerNewsFeedbackCode"]
            ?? throw new InvalidOperationException("Hacker News API code not configured");

        // Get comments from the Hacker News API
        var getFeedbackUrl = $"{BaseUrl}/api/GetHackerNewsFeedback?code={Uri.EscapeDataString(hnCode)}&ids={Uri.EscapeDataString(processedIds)}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        // Analyze the comments
        var markdownResult = await AnalyzeComments("HackerNews", responseContent);

        return (markdownResult, null);
    }
}