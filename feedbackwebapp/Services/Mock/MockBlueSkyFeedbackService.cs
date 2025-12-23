using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Services.Mock;
using FeedbackWebApp.Services.Interfaces;

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
        IAuthenticationHeaderService authenticationHeaderService,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authenticationHeaderService, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null)
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock BlueSky comments...");
        await Task.Delay(1000); // Simulate network delay

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
                            ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567890",
                            Replies = new List<BlueSkyFeedbackItem>
                            {
                                new()
                                {
                                    Id = "at://did:plc:abcdef/app.bsky.feed.post/1234567893",
                                    Author = "did:plc:feedbackflow",
                                    AuthorName = "FeedbackFlow",
                                    AuthorUsername = "feedbackflow.bsky.social",
                                    Content = "Yes! You can customize analysis prompts for your specific needs. 👍",
                                    TimestampUtc = DateTime.UtcNow.AddMinutes(-15),
                                    ParentId = "at://did:plc:abcdef/app.bsky.feed.post/1234567892"
                                }
                            }
                        }
                    }
                }
            }
        };

        // Build comments string
        var comments = string.Join("\n\n", mockFeedback.Items.Select(item =>
        {
            var replies = item.Replies?.Select(r =>
            {
                var nestedReplies = r.Replies?.Select(nr =>
                    $"    > {nr.AuthorName} (@{nr.AuthorUsername}): {nr.Content}") ?? Array.Empty<string>();
                
                return $"  > {r.AuthorName} (@{r.AuthorUsername}): {r.Content}\n{string.Join("\n", nestedReplies)}";
            }) ?? Array.Empty<string>();

            return $"{item.AuthorName} (@{item.AuthorUsername}): {item.Content}\n{string.Join("\n", replies)}";
        }));

        var totalComments = mockFeedback.Items.Sum(item => 
            1 + (item.Replies?.Sum(r => 1 + (r.Replies?.Count ?? 0)) ?? 0));

        return (comments, totalComments, mockFeedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing BlueSky feedback...");
        await Task.Delay(1000); // Simulate analysis time

        var feedback = additionalData as BlueSkyFeedbackResponse;
        var totalComments = commentCount ?? feedback?.Items.Sum(item => 
            1 + (item.Replies?.Sum(r => 1 + (r.Replies?.Count ?? 0)) ?? 0)) ?? 0;

        // Use shared mock analysis provider
        var mockAnalysis = MockAnalysisProvider.GetMockAnalysis("bluesky", totalComments);

        return (mockAnalysis, feedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
