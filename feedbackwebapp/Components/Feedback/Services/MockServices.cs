using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Components.Feedback.Services;

public class MockYouTubeFeedbackService : FeedbackService, IYouTubeFeedbackService
{
    public MockYouTubeFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching YouTube comments...");
        await Task.Delay(1500); // Simulate network delay

        var mockVideos = new List<YouTubeOutputVideo>
        {
            new()
            {
                Id = "mock1",
                Title = "Mock Video 1",
                Url = "https://youtube.com/watch?v=mock1",
                Comments = new List<YouTubeOutputComment>
                {
                    new() { Id = "c1", Author = "User1", Text = "Great video!", PublishedAt = DateTime.Now.AddDays(-1) },
                    new() { Id = "c2", Author = "User2", Text = "Could you explain X more?", PublishedAt = DateTime.Now.AddDays(-2) }
                }
            },
            new()
            {
                Id = "mock2",
                Title = "Mock Video 2",
                Url = "https://youtube.com/watch?v=mock2",
                Comments = new List<YouTubeOutputComment>
                {
                    new() { Id = "c3", Author = "User3", Text = "This helped a lot!", PublishedAt = DateTime.Now.AddDays(-3) }
                }
            }
        };

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing YouTube comments...");
        await Task.Delay(2000); // Simulate AI analysis time

        const string mockMarkdown = @"# Analysis Summary

## Overall Sentiment
Very positive engagement with constructive feedback.

## Key Points
- Users found the content helpful
- Some requests for more detailed explanations
- High engagement rate

## Suggestions
- Consider creating follow-up videos
- More in-depth explanations requested";

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return (mockMarkdown, mockVideos);
    }
}

public class MockHackerNewsFeedbackService : FeedbackService, IHackerNewsFeedbackService
{
    public MockHackerNewsFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching Hacker News comments...");
        await Task.Delay(1500); // Simulate network delay

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing Hacker News discussion...");
        await Task.Delay(2000); // Simulate AI analysis time

        const string mockMarkdown = @"# HackerNews Discussion Analysis

## Overview
High quality technical discussion with valuable insights.

## Key Technical Points
- Discussion of scalability approaches
- Security considerations raised
- Performance optimization suggestions

## Notable Insights
- Several users shared real-world implementation experiences
- Good balance of theoretical and practical perspectives";

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return (mockMarkdown, null);
    }
}