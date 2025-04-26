using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Utils;

namespace FeedbackWebApp.Services.Feedback;

/// <summary>
/// Service for fetching and analyzing BlueSky feedback
/// </summary>
public class BlueSkyFeedbackService : FeedbackService, IBlueSkyFeedbackService
{
    private readonly string _postUrlOrId;

    public BlueSkyFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        string postUrlOrId,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _postUrlOrId = postUrlOrId;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        if (string.IsNullOrWhiteSpace(_postUrlOrId))
            throw new InvalidOperationException("Please enter a valid BlueSky post URL or ID");

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching BlueSky feedback...");

        var blueSkyCode = Configuration["FeedbackApi:GetBlueSkyFeedbackCode"]
            ?? throw new InvalidOperationException("BlueSky API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();
        var getFeedbackUrl = $"{BaseUrl}/api/GetBlueSkyFeedback?code={Uri.EscapeDataString(blueSkyCode)}&post={Uri.EscapeDataString(_postUrlOrId)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();

        var feedback = JsonSerializer.Deserialize<BlueSkyFeedbackResponse>(responseContent);
        if (feedback == null || feedback.Items == null || feedback.Items.Count == 0)
            throw new InvalidOperationException("No feedback found for the specified post");

        var totalComments = CountCommentsRecursively(feedback.Items);

        int CountCommentsRecursively(List<BlueSkyFeedbackItem> items)
        {
            if (items == null) return 0;
            
            return items.Sum(item => 1 + CountCommentsRecursively(item.Replies ?? new()));
        }
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Found {totalComments} comments and replies...");

        // Analyze comments using the shared AnalyzeComments method
        var markdown = await AnalyzeComments("bluesky", responseContent, totalComments);

        return (markdown, feedback);
    }
}
