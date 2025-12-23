using System.Text.Json;
using SharedDump.Models.TwitterFeedback;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services.Feedback;

public class TwitterFeedbackService : FeedbackService, ITwitterFeedbackService
{
    private readonly string _tweetUrlOrId;

    public TwitterFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        string tweetUrlOrId,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
    {
        _tweetUrlOrId = tweetUrlOrId;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        if (string.IsNullOrWhiteSpace(_tweetUrlOrId))
            throw new InvalidOperationException("Please enter a valid tweet URL or ID");

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Twitter/X feedback...");

        var twitterCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("Twitter API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();
        var getFeedbackUrl = $"{BaseUrl}/api/GetTwitterFeedback?code={Uri.EscapeDataString(twitterCode)}&tweet={Uri.EscapeDataString(_tweetUrlOrId)}&maxComments={maxComments}";
        var feedbackResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Get, getFeedbackUrl);
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        var feedback = JsonSerializer.Deserialize<TwitterFeedbackResponse>(responseContent);

        if (feedback == null || feedback.Items == null || feedback.Items.Count == 0)
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("No comments available", 0, null);
        }

        int CountRepliesRecursively(List<TwitterFeedbackItem> items)
        {
            if (items == null || items.Count == 0)
                return 0;

            int count = items.Count;

            foreach (var item in items)
            {
                if (item.Replies != null)
                {
                    count += CountRepliesRecursively(item.Replies);
                }
            }

            return count;
        }

        var totalComments = CountRepliesRecursively(feedback.Items);



        return (responseContent, totalComments, feedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        var totalComments = commentCount ?? comments.Split("\n").Count(line => line.StartsWith("Tweet by") || line.StartsWith("Reply by"));
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {totalComments} tweets and replies...");

        // Use minified data format if we have additionalData, otherwise fall back to string format
        string markdownResult;
        if (additionalData != null)
        {
            markdownResult = await AnalyzeCommentsInternalWithMinifiedData("twitter", additionalData, totalComments);
        }
        else
        {
            markdownResult = await AnalyzeCommentsInternal("twitter", comments, totalComments);
        }
        
        return (markdownResult, additionalData);
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
