using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.BlueSkyFeedback;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of BlueSky feedback service for testing and development
/// </summary>
public class MockBlueSkyFeedbackService : FeedbackService, IBlueSkyFeedbackService
{
    public MockBlueSkyFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock BlueSky data...");
        await Task.Delay(500); // Simulate network delay

        var mockFeedback = new BlueSkyFeedbackResponse
        {
            Items = new List<BlueSkyFeedbackItem>
            {
                new BlueSkyFeedbackItem
                {
                    Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567890",
                    Author = "did:plc:feedbackflow",
                    AuthorName = "FeedbackFlow",
                    AuthorUsername = "feedbackflow.bsky.social",
                    Content = "Excited to announce support for BlueSky in FeedbackFlow!",
                    TimestampUtc = DateTime.UtcNow.AddMinutes(-30),
                    Replies = new List<BlueSkyFeedbackItem>
                    {
                        new BlueSkyFeedbackItem
                        {
                            Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567891",
                            Author = "did:plc:user1",
                            AuthorName = "Skyline Developer",
                            AuthorUsername = "skydev.bsky.social",
                            Content = "Congrats! Looking forward to using this feature.",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-28),
                            ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567890"
                        },
                        new BlueSkyFeedbackItem
                        {
                            Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567892",
                            Author = "did:plc:user2",
                            AuthorName = "Blazor Enthusiast",
                            AuthorUsername = "blazorfan.bsky.social",
                            Content = "This is exactly what I was looking for!",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-25),
                            ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567890"
                        }
                    }
                }
            }
        };

        var totalComments = mockFeedback.Items.Sum(item => 1 + (item.Replies?.Count ?? 0));
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Found {totalComments} comments and replies (mock)...");

        var markdown = "# BlueSky Feedback (Mock)\n\n" +
            "## Overall Sentiment & Engagement 💙\n\n" +
            "Strong positive sentiment with users excited about the BlueSky integration. Good engagement with quick replies.\n\n" + 
            "## Key Takeaways\n\n" +
            "- Users are enthusiastic about the integration\n" +
            "- Developers are looking forward to trying the feature\n" +
            "- No negative feedback observed\n\n" +
            string.Join("\n\n", mockFeedback.Items.Select(item => $"**{item.AuthorName} (@{item.AuthorUsername})**: {item.Content}\n{string.Join("\n", item.Replies?.Select(r => $"> **{r.AuthorName} (@{r.AuthorUsername})**: {r.Content}") ?? Array.Empty<string>())}"));

        return (markdown, mockFeedback);
    }
}
