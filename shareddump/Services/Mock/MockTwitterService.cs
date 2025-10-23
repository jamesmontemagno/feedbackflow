using SharedDump.Models.TwitterFeedback;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of Twitter service for testing
/// </summary>
public class MockTwitterService : ITwitterService
{
    public async Task<TwitterFeedbackResponse?> GetTwitterThreadAsync(string tweetUrlOrId)
    {
        await Task.Delay(1000); // Simulate network delay

        return new TwitterFeedbackResponse
        {
            Items = new List<TwitterFeedbackItem>
            {
                new TwitterFeedbackItem
                {
                    Id = "1234567890",
                    Author = "@feedbackflow",
                    AuthorName = "FeedbackFlow",
                    Content = "🚀 Excited to announce our latest release! Built with #dotnet and @blazor, now with improved performance and even better feedback analysis!",
                    TimestampUtc = DateTime.UtcNow.AddHours(-4),
                    Replies = new List<TwitterFeedbackItem>
                    {
                        new TwitterFeedbackItem
                        {
                            Id = "1234567891",
                            Author = "@devuser",
                            AuthorName = "Developer",
                            Content = "Love the new features! How's the performance with large datasets?",
                            TimestampUtc = DateTime.UtcNow.AddHours(-3),
                            ParentId = "1234567890"
                        },
                        new TwitterFeedbackItem
                        {
                            Id = "1234567892",
                            Author = "@feedbackflow",
                            AuthorName = "FeedbackFlow",
                            Content = "Thanks! We've optimized for large datasets - processing 10k+ comments is now 3x faster!",
                            TimestampUtc = DateTime.UtcNow.AddHours(-2),
                            ParentId = "1234567890"
                        }
                    }
                }
            }
        };
    }
    
    public async Task<List<TwitterFeedbackItem>> SearchTweetsAsync(string query, int maxResults = 25, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken); // Simulate network delay
        return new List<TwitterFeedbackItem>
        {
            new TwitterFeedbackItem
            {
                Id = "987654321",
                Author = "@devuser1",
                AuthorName = "Developer User 1",
                AuthorUsername = "devuser1",
                Content = $"Found this great article about {query}: https://example.com/article1",
                TimestampUtc = DateTime.UtcNow.AddDays(-1),
                Replies = new List<TwitterFeedbackItem>()
            },
            new TwitterFeedbackItem
            {
                Id = "987654322",
                Author = "@devuser2",
                AuthorName = "Developer User 2",
                AuthorUsername = "devuser2",
                Content = $"Working with {query} has been a game changer for our team!",
                TimestampUtc = DateTime.UtcNow.AddDays(-2),
                Replies = new List<TwitterFeedbackItem>()
            }
        };
    }
}
