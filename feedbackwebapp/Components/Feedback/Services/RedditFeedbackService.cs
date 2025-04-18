using System.Net.Http.Json;
using SharedDump.Models.Reddit;

namespace FeedbackWebApp.Components.Feedback.Services;

public class RedditFeedbackService : FeedbackService, IRedditFeedbackService
{
    private readonly string[] _threadIds;

    public RedditFeedbackService(
        string[] threadIds,
        HttpClient http,
        IConfiguration configuration,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, onStatusUpdate)
    {
        _threadIds = threadIds;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        if (_threadIds.Length == 0)
        {
            throw new InvalidOperationException("No thread IDs provided");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Reddit threads...");

        var threads = new List<RedditThreadModel>();
        var getRedditCode = Configuration["FeedbackApi:GetRedditFeedbackCode"] 
            ?? throw new InvalidOperationException("Reddit API code not configured");

        foreach (var threadId in _threadIds)
        {
            try
            {
                var getFeedbackUrl = $"{BaseUrl}/api/GetRedditFeedback?code={Uri.EscapeDataString(getRedditCode)}&threads={Uri.EscapeDataString(threadId)}";

                var stringResponse = await Http.GetStringAsync(getFeedbackUrl);
                var response = System.Text.Json.JsonSerializer.Deserialize<RedditThreadModel[]>(stringResponse);

                
                if (response != null && response.Length > 0)
                {
                    threads.AddRange(response);
                }
            }
            catch (Exception)
            {
                throw; // Properly re-throw the exception without losing stack trace
            }
        }

        if (!threads.Any())
        {
            throw new InvalidOperationException("No Reddit threads found");
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing Reddit comments...");
        
        var commentsJson = System.Text.Json.JsonSerializer.Serialize(threads);
        var analysis = await AnalyzeComments("reddit", commentsJson);

        return (analysis, threads);
    }
}