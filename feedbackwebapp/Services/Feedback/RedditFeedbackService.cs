using SharedDump.Models.Reddit;
using System.Text.Json;
using SharedDump.Utils;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public class RedditFeedbackService : FeedbackService, IRedditFeedbackService
{
    private readonly string[] _threadIds;

    public RedditFeedbackService(
        string[] threadIds,
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        _threadIds = threadIds;
    }

    private int CountCommentsAndReplies(List<RedditCommentModel>? comments)
    {
        if (comments == null || !comments.Any()) return 0;
        return comments.Count + comments.Sum(c => CountCommentsAndReplies(c.Replies));
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        var processedIds = UrlParsing.ExtractRedditId(_threadIds);

        if (string.IsNullOrWhiteSpace(processedIds))
        {
            throw new InvalidOperationException("Please enter at least one valid Reddit thread ID or URL");
        }

        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Reddit comments...");

        var redditCode = Configuration["FeedbackApi:GetRedditFeedbackCode"]
            ?? throw new InvalidOperationException("Reddit API code not configured");

        var maxComments = await GetMaxCommentsToAnalyze();

        // Get comments from the Reddit API
        var getFeedbackUrl = $"{BaseUrl}/api/GetRedditFeedback?code={Uri.EscapeDataString(redditCode)}&threads={Uri.EscapeDataString(processedIds)}&maxComments={maxComments}";
        var feedbackResponse = await Http.GetAsync(getFeedbackUrl);
        feedbackResponse.EnsureSuccessStatusCode();
        
        var responseContent = await feedbackResponse.Content.ReadAsStringAsync();
        // Parse the Reddit response
        var threads = JsonSerializer.Deserialize<List<RedditThreadModel>>(responseContent);

        if (threads == null || !threads.Any())
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("No comments available", 0, null);
        }

        var totalComments = threads.Sum(t => CountCommentsAndReplies(t.Comments));
        UpdateStatus(FeedbackProcessStatus.GatheringComments, $"Found {totalComments} comments...");

        // Build the comments string
        var allComments = string.Join("\n\n", threads.Select(t =>
        {
            var threadComments = new List<string>();
            void AddComment(RedditCommentModel comment, int depth = 0)
            {
                var indent = new string(' ', depth * 2);
                threadComments.Add($"{indent}Comment by {comment.Author}: {comment.Body}");
                
                if (comment.Replies?.Any() == true)
                {
                    foreach (var reply in comment.Replies)
                    {
                        AddComment(reply, depth + 1);
                    }
                }
            }

            threadComments.Add($"Thread: {t.Title}\nAuthor: {t.Author}\nContent: {t.SelfText}");
            foreach (var comment in t.Comments)
            {
                AddComment(comment);
            }

            return string.Join("\n", threadComments);
        }));

        return (allComments, totalComments + threads.Count, threads); // Add threads.Count to include original posts
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        var totalComments = commentCount ?? comments.Split("\n").Count(line => line.Contains("Comment by") || line.StartsWith("Thread:"));
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {totalComments} comments...");

        // Analyze comments
        var markdownResult = await AnalyzeCommentsInternal("reddit", comments, totalComments);
        return (markdownResult, additionalData);
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