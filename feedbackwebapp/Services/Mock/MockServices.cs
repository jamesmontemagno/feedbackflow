using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Services.Mock;

public class MockYouTubeFeedbackService(
    HttpClient http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, onStatusUpdate), IYouTubeFeedbackService
{
    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock YouTube comments...");
        await Task.Delay(1000); // Simulate network delay

        var mockVideos = new List<YouTubeOutputVideo>
        {
            new()
            {
                Id = "abc123",
                Title = "Sample Video 1",
                Comments =
                [
                    new()
                    { 
                        Id = "comment1",
                        Author = "User1",
                        Text = "Great video! Very informative.",
                        PublishedAt = DateTime.UtcNow.AddDays(-1)
                    },
                    new()
                    { 
                        Id = "comment2",
                        Author = "User2",
                        Text = "Could you make a follow-up on this topic?",
                        PublishedAt = DateTime.UtcNow.AddDays(-2)
                    }
                ]
            },
            new()
            {
                Id = "xyz789",
                Title = "Sample Video 2",
                Comments =
                [
                    new()
                    { 
                        Id = "comment3",
                        Author = "User3",
                        Text = "Excellent explanation, helped me understand the concept.",
                        PublishedAt = DateTime.UtcNow.AddDays(-3)
                    },
                    new()
                    { 
                        Id = "comment4",
                        Author = "User4",
                        Text = "The examples at 5:30 were really helpful.",
                        PublishedAt = DateTime.UtcNow.AddDays(-4)
                    }
                ]
            }
        };

        var allComments = string.Join("\n\n", mockVideos.SelectMany(v => 
            v.Comments.Select(c => $"Video: {v.Title}\nComment by {c.Author}: {c.Text}")));

        var markdownResult = await AnalyzeComments("youtube", allComments, mockVideos.Count);
        return (markdownResult, mockVideos);
    }
}

public class MockHackerNewsFeedbackService(
    HttpClient http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, onStatusUpdate), IHackerNewsFeedbackService
{
    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Hacker News comments...");
        await Task.Delay(1000);

        var mockComments = "Story: Understanding TypeScript's Type System\n" +
            "Comment by user1: TypeScript has been a game changer for our team's productivity.\n" +
            "Comment by user2: The strict mode is essential for catching potential issues early.\n" +
            "Comment by user3: Great write-up, but I think you missed covering mapped types.";

        var markdownResult = await AnalyzeComments("hackernews", mockComments, mockComments.Count());
        return (markdownResult, null);
    }
}