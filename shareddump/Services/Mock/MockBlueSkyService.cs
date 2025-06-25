using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of BlueSky service for testing
/// </summary>
public class MockBlueSkyService : IBlueSkyService
{
    public async Task<BlueSkyFeedbackResponse?> GetBlueSkyPostAsync(string postUrlOrId)
    {
        await Task.Delay(1000); // Simulate network delay

        return new BlueSkyFeedbackResponse
        {
            Items = new List<BlueSkyFeedbackItem>
            {
                new BlueSkyFeedbackItem
                {
                    Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567890",
                    Author = "did:plc:feedbackflow",
                    AuthorName = "FeedbackFlow",
                    AuthorUsername = "feedbackflow.bsky.social",
                    Content = "Excited to announce BlueSky support in FeedbackFlow! 🚀",
                    TimestampUtc = DateTime.UtcNow.AddMinutes(-30),
                    Replies = new List<BlueSkyFeedbackItem>
                    {
                        new()
                        {
                            Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567891",
                            Author = "did:plc:user1",
                            AuthorName = "Early Adopter",
                            AuthorUsername = "earlyadopter.bsky.social",
                            Content = "Finally! Just what I needed for my feedback analysis.",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-25),
                            ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567890"
                        },
                        new()
                        {
                            Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567892",
                            Author = "did:plc:user2",
                            AuthorName = "Developer",
                            AuthorUsername = "developer.bsky.social",
                            Content = "Does it support custom analysis prompts? 🤔",
                            TimestampUtc = DateTime.UtcNow.AddMinutes(-20),
                            ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567890"
                        }
                    }
                }
            }
        };
    }

    public void SetCredentials(string username, string appPassword)
    {
        // Mock implementation - no actual credentials needed
    }
}
