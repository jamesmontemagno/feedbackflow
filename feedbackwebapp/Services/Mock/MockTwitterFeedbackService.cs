using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.TwitterFeedback;

namespace FeedbackWebApp.Services.Mock;

public class MockTwitterFeedbackService : FeedbackService, ITwitterFeedbackService
{
    public MockTwitterFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Twitter/X data...");
        await Task.Delay(500); // Simulate network delay

        var mockFeedback = new TwitterFeedbackResponse
        {
            Items = new List<TwitterFeedbackItem>
            {
                new TwitterFeedbackItem
                {
                    Id = "1234567890",
                    Author = "@dotnetdev",
                    Content = "Excited to announce .NET 9!",
                    TimestampUtc = DateTime.UtcNow.AddMinutes(-30),
                    Replies = new List<TwitterFeedbackItem>
                    {
                        new TwitterFeedbackItem
                        {
                            Id = "1234567891",
                            Author = "@csharpfan",
                            Content = "Congrats! Looking forward to the new features.",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-28),
                            ParentId = "1234567890"
                        },
                        new TwitterFeedbackItem
                        {
                            Id = "1234567892",
                            Author = "@blazorenthusiast",
                            Content = "Will Blazor get more love in this release?",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-25),
                            ParentId = "1234567890"
                        }
                    }
                }
            }
        };

        var totalComments = mockFeedback.Items.Sum(item => 1 + (item.Replies?.Count ?? 0));
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Found {totalComments} comments and replies (mock)...");

        var markdown = "# Twitter Feedback (Mock)\n\n" +
            string.Join("\n\n", mockFeedback.Items.Select(item => $"**{item.Author}**: {item.Content}\n{string.Join("\n", item.Replies?.Select(r => $"> **{r.Author}**: {r.Content}") ?? Array.Empty<string>())}"));

        return (markdown, mockFeedback);
    }
}
