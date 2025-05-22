using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Services.Mock;

public class MockYouTubeFeedbackService(
    IHttpClientFactory http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, onStatusUpdate), IYouTubeFeedbackService
{    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
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

        // Build our comments string and count total comments
        var totalComments = mockVideos.Sum(v => v.Comments?.Count ?? 0);
        var allComments = string.Join("\n\n", mockVideos.SelectMany(v => 
            v.Comments.Select(c => $"Video: {v.Title}\nComment by {c.Author}: {c.Text}")));

        return (allComments, totalComments, mockVideos);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        // Simulate analysis time
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock YouTube comments...");
        await Task.Delay(1000); 
        
        var mockMarkdownResult = @$"# YouTube Comment Analysis 📺

## Overall Sentiment & Engagement 👍
- Overwhelmingly positive reception across both videos
- High engagement with specific content sections
- Active viewer participation requesting follow-up content

## Key Highlights & Timestamps 🔍
- Example section at 5:30 frequently mentioned as particularly helpful
- Viewers found explanations clear and valuable
- Content successfully helping viewers understand complex concepts

## Community Feedback 💭
- Users find content informative and well-explained
- Positive sentiment around teaching style
- Follow-up content requests indicate high interest

## Recommendations 📝
- Consider creating follow-up content
- Highlight successful teaching approaches
- Maintain current explanation style that viewers found effective";

        return (mockMarkdownResult, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}

public class MockHackerNewsFeedbackService(
    IHttpClientFactory http,
    IConfiguration configuration,
    UserSettingsService userSettings,
    FeedbackStatusUpdate? onStatusUpdate = null)
    : FeedbackService(http, configuration, userSettings, onStatusUpdate), IHackerNewsFeedbackService
{
    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Hacker News comments...");
        await Task.Delay(1000); // Simulate network delay

        var mockComments = @"Story: Announcing TypeScript 5.4: The Best Release Yet for Developer Experience
Comment by user123: The new type inference improvements are incredible. This will save me hours of debugging.
Comment by devExpert: I've been testing the beta, and the strict mode enhancements are a game changer for large codebases.
Comment by typescript_fan: Would love to see more examples of using the new mapped types features.
Comment by senior_dev: The performance improvements in type checking are noticeable in our monorepo.

Story: Why We Chose TypeScript for Our New Project
Comment by jsDev: TypeScript has been essential for maintaining our codebase as it grows.
Comment by tech_lead: The biggest win for us was catching errors before runtime.
Comment by newbie: As someone learning TS, the documentation and community support are fantastic.
Comment by architect: Strong typing has improved our team's productivity significantly.";

        // Create a mock HackerNews response structure
        var mockArticleThreads = new List<List<HackerNewsItem>>
        {
            new()
            {
                new()
                {
                    Id = 123,
                    Title = "Announcing TypeScript 5.4: The Best Release Yet for Developer Experience",
                    By = "microsoftdev",
                    Text = null,
                    Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Kids = new List<int> { 456, 457, 458, 459 }
                },
                new()
                {
                    Id = 456,
                    By = "user123",
                    Text = "The new type inference improvements are incredible. This will save me hours of debugging.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds(),
                    Parent = 123
                }
                // Additional comments would be added here
            },
            new()
            {
                new()
                {
                    Id = 124,
                    Title = "Why We Chose TypeScript for Our New Project",
                    By = "teamlead",
                    Text = null,
                    Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Kids = new List<int> { 460, 461, 462, 463 }
                },
                new()
                {
                    Id = 460,
                    By = "jsDev",
                    Text = "TypeScript has been essential for maintaining our codebase as it grows.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-45).ToUnixTimeSeconds(),
                    Parent = 124
                }
                // Additional comments would be added here
            }
        };

        // Count total comments by counting lines that start with "Comment by"
        int commentCount = mockComments.Split('\n').Count(line => line.StartsWith("Comment by"));

        return (mockComments, commentCount, mockArticleThreads);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock Hacker News comments...");
        await Task.Delay(1000); // Simulate analysis time
        
        // Use the provided comment count or calculate it
        int totalComments = commentCount ?? comments.Split('\n').Count(line => line.StartsWith("Comment by"));
        
        var mockMarkdownResult = @$"# Hacker News Discussion Analysis: TypeScript's Type System 📊

## Overview and Key Themes 🔍
The discussion focuses on TypeScript's type system, with strong community support for its benefits in development workflows. Analysis based on {totalComments} comments covering two main threads.

## Technical Highlights ⚡
1. Type Inference Improvements
- Positive feedback on new inference capabilities
- Reduced debugging time reported
- Enhanced IDE support

2. Strict Mode Features
- Particularly valuable for large codebases
- Improved error detection
- Value of strict mode for error prevention
- Interest in advanced type system features

## Feature Popularity 🌟
1. **Strict Mode**: Most frequently mentioned feature with positive sentiment
2. **Static Type Checking**: Highlighted for preventing runtime errors
3. **Advanced Types**: Community interest in mapped types and other advanced features

## Learning & Documentation
- Positive feedback on documentation quality
- Strong community support noted
- Resources available for newcomers

## Impact on Development
1. Improved maintainability
2. Better error detection
3. Enhanced team productivity
4. Stronger tooling support

## Sentiment Analysis 💬
Analysis of {totalComments} comments shows:
- Strongly positive sentiment (90% favorable)
- High engagement level
- Constructive discussion quality

The Hacker News community strongly endorses TypeScript's type system, particularly valuing strict mode for error prevention. There's notable interest in advanced typing features like mapped types, suggesting an opportunity for more educational content in this area.";

        return (mockMarkdownResult, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}