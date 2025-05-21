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

    public override async Task<(string rawComments, object? additionalData)> GetComments()
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

        return (comments, mockFeedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing Twitter/X feedback...");
        await Task.Delay(1000); // Simulate analysis time

        var feedback = additionalData as TwitterFeedbackResponse;
        var totalComments = feedback?.Items.Sum(item => 1 + (item.Replies?.Count ?? 0)) ?? 0;
        var uniqueAuthors = feedback?.Items
            .Union(feedback.Items.SelectMany(i => i.Replies ?? new List<TwitterFeedbackItem>()))
            .Select(i => i.Author)
            .Distinct()
            .Count() ?? 0;

        var mockAnalysis = @$"# Twitter/X Feedback Analysis 🐦

## Overview
Total Interactions: {totalComments} tweets and replies
Unique Participants: {uniqueAuthors}
Time Range: Last 4 hours
Overall Sentiment: Very Positive

## Key Themes 🎯

### Product Features
- Performance improvements highlighted
- Large dataset handling capabilities
- Weekly feedback review functionality

### User Experience
- Positive sentiment around insights
- Strong appreciation for analysis capabilities
- Easy integration into workflows

### Use Cases 💡
1. Product Development
   - Feature prioritization
   - Weekly feedback reviews
   - Performance monitoring

2. Technical Implementation
   - Large dataset processing
   - Integration capabilities
   - Performance benchmarks

## Engagement Metrics 📊
- Active discussions around features
- Quick response time to inquiries
- Strong professional user engagement

## Community Feedback
- Product managers finding value
- Developers interested in technical details
- Positive feedback on performance improvements

## Recommendations 📋
1. Share more performance metrics
2. Highlight use cases in documentation
3. Consider creating case studies
4. Continue engaging with technical queries

### Future Engagement
- Monitor for feature requests
- Track performance feedback
- Share success stories
- Engage with product teams";

        return (mockAnalysis, feedback);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, additionalData);
    }
}
