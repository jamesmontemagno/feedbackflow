using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.BlueSkyFeedback;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

/// <summary>
/// Service for fetching and analyzing BlueSky feedback
/// </summary>
public class BlueSkyFeedbackService : FeedbackService, IBlueSkyFeedbackService
{
    private readonly string _postUrlOrId;

    public BlueSkyFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        string postUrlOrId,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
    {
        _postUrlOrId = postUrlOrId;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        if (string.IsNullOrWhiteSpace(_postUrlOrId))
            throw new InvalidOperationException("Please enter a valid BlueSky post URL or ID");

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching BlueSky feedback...");

        var blueSkyCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("BlueSky API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();
        var getFeedbackUrl = $"{BaseUrl}/api/GetBlueSkyFeedback?code={Uri.EscapeDataString(blueSkyCode)}&post={Uri.EscapeDataString(_postUrlOrId)}&maxComments={maxComments}";
        var feedbackResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Get, getFeedbackUrl);
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        
        var feedback = JsonSerializer.Deserialize<BlueSkyFeedbackResponse>(responseContent);
        var totalComments = feedback?.Items != null ? CountCommentsRecursively(feedback.Items) : 0;
        
        return (responseContent, totalComments, feedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        var feedback = JsonSerializer.Deserialize<BlueSkyFeedbackResponse>(comments);
        if (feedback == null || feedback.Items == null || feedback.Items.Count == 0)
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        var totalComments = commentCount ?? CountCommentsRecursively(feedback.Items);
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Found {totalComments} comments and replies...");

        // Analyze comments using the shared AnalyzeComments method
        var markdown = await AnalyzeCommentsInternal("bluesky", comments, totalComments);
        return (markdown, feedback);
    }

    private int CountCommentsRecursively(List<BlueSkyFeedbackItem> items)
    {
        if (items == null) return 0;
        return items.Sum(item => 1 + CountCommentsRecursively(item.Replies ?? new()));
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
