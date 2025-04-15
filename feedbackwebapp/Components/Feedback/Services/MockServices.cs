using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Components.Feedback.Services;

public class MockYouTubeFeedbackService : IYouTubeFeedbackService
{
    public Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
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

        object? additionalData = mockVideos;
        return Task.FromResult((mockMarkdown, additionalData));
    }
}

public class MockHackerNewsFeedbackService : IHackerNewsFeedbackService
{
    public Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
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

        return Task.FromResult((mockMarkdown, (object?)null));
    }
}