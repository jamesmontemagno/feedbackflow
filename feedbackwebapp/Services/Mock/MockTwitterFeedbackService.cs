using System.Text.Json;
using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Services.Mock;

namespace FeedbackWebApp.Services.Mock;

public class MockTwitterFeedbackService : FeedbackService, ITwitterFeedbackService
{
    public MockTwitterFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        IAuthenticationHeaderService authenticationHeaderService,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null)
    {
        try
        {
            UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Twitter/X comments...");
            await Task.Delay(1000); // Simulate network delay

            var mockFeedback = new TwitterFeedbackResponse
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
                                TimestampUtc = DateTime.UtcNow.AddHours(-3),
                                ParentId = "1234567890"
                            }
                        }
                    },
                    new TwitterFeedbackItem
                    {
                        Id = "1234567893",
                        Author = "@techexplorer",
                        AuthorName = "Tech Explorer",
                        Content = "Just tried @feedbackflow for our product feedback analysis. The insights are amazing! #ProductDevelopment",
                        TimestampUtc = DateTime.UtcNow.AddHours(-2),
                        Replies = new List<TwitterFeedbackItem>
                        {
                            new TwitterFeedbackItem
                            {
                                Id = "1234567894",
                                Author = "@productmanager",
                                AuthorName = "Product Lead",
                                Content = "Agreed! We're using it for our weekly feedback reviews. Super helpful for prioritizing features.",
                                TimestampUtc = DateTime.UtcNow.AddHours(-1),
                                ParentId = "1234567893"
                            }
                        }
                    }
                }
            };

            // Build comments string
            var commentsList = new List<string>();
            foreach (var item in mockFeedback.Items)
            {
                var responses = item.Replies?.Select(r => $"  > {r.AuthorName} (@{r.Author}): {r.Content}") ?? Array.Empty<string>();
                commentsList.Add($"{item.AuthorName} (@{item.Author}): {item.Content}\n{string.Join("\n", responses)}");
            }
            var comments = string.Join("\n\n", commentsList);

            // Count total comments including replies
            int totalComments = mockFeedback.Items.Sum(i => 1 + (i.Replies?.Count ?? 0));

            return (comments, totalComments, mockFeedback);
        }
        catch (Exception)
        {
            // Re-throw to maintain the exception for proper error handling in the calling code
            throw;
        }
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing Twitter/X feedback...");
        await Task.Delay(1000); // Simulate analysis time

        var feedback = additionalData as TwitterFeedbackResponse;
        var totalComments = commentCount ?? feedback?.Items.Sum(i => 1 + (i.Replies?.Count ?? 0)) ?? 0;

        // Use shared mock analysis provider
        var mockAnalysis = MockAnalysisProvider.GetMockAnalysis("twitter", totalComments);

        return (mockAnalysis, feedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments with count
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
