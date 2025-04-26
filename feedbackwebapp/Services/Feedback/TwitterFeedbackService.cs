using System.Net.Http.Json;
using System.Text.Json;
using SharedDump.Models.TwitterFeedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class TwitterFeedbackService : FeedbackService, ITwitterFeedbackService
{
    private readonly string _tweetUrlOrId;

    public TwitterFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        string tweetUrlOrId,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _tweetUrlOrId = tweetUrlOrId;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        if (string.IsNullOrWhiteSpace(_tweetUrlOrId))
            throw new InvalidOperationException("Please enter a valid tweet URL or ID");

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Twitter/X feedback...");

        var twitterCode = Configuration["FeedbackApi:GetTwitterFeedbackCode"]
            ?? throw new InvalidOperationException("Twitter API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();
        var getFeedbackUrl = $"{BaseUrl}/api/GetTwitterFeedback?code={Uri.EscapeDataString(twitterCode)}&tweet={Uri.EscapeDataString(_tweetUrlOrId)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        var feedback = JsonSerializer.Deserialize<TwitterFeedbackResponse>(responseContent);
        if (feedback == null || feedback.Items == null || feedback.Items.Count == 0)
            throw new InvalidOperationException("No feedback found for the specified tweet");

        var totalComments = CountCommentsRecursively(feedback.Items);

        int CountCommentsRecursively(List<TwitterFeedbackItem> items)
        {
            if (items == null) return 0;
            
            return items.Sum(item => 1 + CountCommentsRecursively(item.Replies ?? new()));
        }
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Found {totalComments} comments and replies...");

       
        // Optionally, analyze comments using the shared AnalyzeComments method
        var markdown = await AnalyzeComments("twitter", responseContent, totalComments);

        return (markdown, feedback);
    }
}
