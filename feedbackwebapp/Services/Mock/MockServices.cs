using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Services.Mock;

public class MockYouTubeFeedbackService(
    IHttpClientFactory http,
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

        // Instead of using AnalyzeComments, return a pre-defined mockup result
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock YouTube comments...");
        await Task.Delay(1000); // Simulate analysis time
        
        var mockMarkdownResult = @"# YouTube Comment Analysis 📺

## Overall Sentiment & Engagement 👍
- Overwhelmingly positive reception across both videos
- High engagement with specific content sections
- Active viewer participation requesting follow-up content

## Key Highlights & Timestamps 🔍
- Example section at 5:30 frequently mentioned as particularly helpful
- Viewers found explanations clear and valuable
- Content successfully helping viewers understand complex concepts

## Positive Feedback Themes ✨
- **Clear Explanations**: Multiple comments praising clarity
- **Informative Content**: Viewers appreciating depth of information
- **Practical Examples**: Specific examples receiving positive mentions

## Suggestions & Requests 💡
- Follow-up content requested on the same topic
- Interest in expanding on concepts introduced

## Questions & Discussion Points ❓
- No significant confusion points identified
- Strong understanding of presented material evident in comments

## Recommendations 📋
- Consider creating follow-up content as requested by viewers
- Continue emphasis on practical examples which resonated strongly
- Maintain current explanation style that viewers found effective";

        return (mockMarkdownResult, mockVideos);
    }
}

public class MockHackerNewsFeedbackService(
    IHttpClientFactory http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, onStatusUpdate), IHackerNewsFeedbackService
{
    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Hacker News comments...");
        await Task.Delay(1000); // Simulate network delay

        // Instead of using AnalyzeComments, return a pre-defined mockup result
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock Hacker News comments...");
        await Task.Delay(1000); // Simulate analysis time
        
        var mockMarkdownResult = @"# Hacker News Discussion Analysis: TypeScript's Type System 📊

## Overview and Key Themes 🔍
The discussion focuses on TypeScript's type system, with strong community support for its benefits in development workflows. Key themes include:
- Productivity improvements when using TypeScript
- Value of strict mode for error prevention
- Interest in advanced type system features

## Sentiment Analysis 💬
**Positive Comments:**
- TypeScript described as a ""game changer"" for team productivity
- Strong appreciation for type safety features
- Recognition of TypeScript's value in catching errors early

**Neutral/Constructive Comments:**
- Request for more coverage on advanced features like mapped types
- Discussion of specific TypeScript configuration options

## Feature Popularity 🌟
1. **Strict Mode**: Most frequently mentioned feature with positive sentiment
2. **Static Type Checking**: Highlighted for preventing runtime errors
3. **Advanced Types**: Community interest in mapped types and other advanced features

## Comparative Analysis ⚖️
- TypeScript favorably compared to plain JavaScript for large codebases
- Strict mode specifically praised over loose type checking
- Some discussion of TypeScript vs. alternative type systems

## Trade-offs and Controversies 🔄
- Minimal controversy detected in the discussion
- General consensus on TypeScript's value proposition
- Some debate about ideal strictness settings for different project sizes

## Recommendations & Opportunities 💡
1. Consider expanding documentation/tutorials on advanced typing features
2. Continue emphasizing strict mode adoption in community resources
3. Provide more examples of mapped types and their practical applications

## Final Summary 📝
The Hacker News community strongly endorses TypeScript's type system, particularly valuing strict mode for error prevention. There's notable interest in advanced typing features like mapped types, suggesting an opportunity for more educational content in this area.";

        return (mockMarkdownResult, null);
    }
}