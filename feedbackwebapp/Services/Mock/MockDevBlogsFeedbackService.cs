using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.DevBlogs;

namespace FeedbackWebApp.Services.Mock;

public class MockDevBlogsFeedbackService : FeedbackService, IDevBlogsFeedbackService
{
    public string ArticleUrl { get; set; } = string.Empty;

    public MockDevBlogsFeedbackService(IHttpClientFactory http, IConfiguration configuration, UserSettingsService userSettings, FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock DevBlogs comments...");
        await Task.Delay(1000); // Simulate network delay

        var mockArticle = new DevBlogsArticleModel
        {
            Title = "Getting Started with FeedbackFlow",
            Url = ArticleUrl,
            Comments = new List<DevBlogsCommentModel>
            {
                new()
                {
                    Id = "1",
                    Author = "DotNetEnthusiast",
                    BodyHtml = "Great post! I especially like how FeedbackFlow integrates with multiple platforms.",
                    PublishedUtc = DateTimeOffset.UtcNow.AddDays(-6),
                    Replies = new List<DevBlogsCommentModel>
                    {
                        new()
                        {
                            Id = "1.1",
                            Author = "CloudDeveloper",
                            BodyHtml = "The Azure integration looks particularly useful. Any plans for AWS support?",
                            PublishedUtc = DateTimeOffset.UtcNow.AddDays(-6),
                            Replies = new List<DevBlogsCommentModel>
                            {
                                new()
                                {
                                    Id = "1.1.1",
                                    Author = "James Montemagno",
                                    BodyHtml = "Thanks for the feedback! We're focusing on Azure integration for now, but we're open to exploring other cloud platforms in the future.",
                                    PublishedUtc = DateTimeOffset.UtcNow.AddDays(-6)
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "2",
                    Author = "BlazeAhead",
                    BodyHtml = "I've been looking for something like this for my Blazor application. The documentation is very clear!",
                    PublishedUtc = DateTimeOffset.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = "3",
                    Author = "PerformancePro",
                    BodyHtml = "How does it handle large volumes of feedback? Any performance benchmarks available?",
                    PublishedUtc = DateTimeOffset.UtcNow.AddDays(-4),
                    Replies = new List<DevBlogsCommentModel>
                    {
                        new()
                        {
                            Id = "3.1",
                            Author = "James Montemagno",
                            BodyHtml = "FeedbackFlow uses efficient batching and async processing for handling large volumes. We'll share some benchmarks in an upcoming post!",
                            PublishedUtc = DateTimeOffset.UtcNow.AddDays(-4)
                        }
                    }
                }
            }
        };

        // Build comments string
        var comments = BuildCommentsString(mockArticle);

        return (comments, mockArticle);
    }

    private static string BuildCommentsString(DevBlogsArticleModel article)
    {
        var commentsList = new List<string>();

        void AddComment(DevBlogsCommentModel comment, int depth = 0)
        {
            var indent = new string(' ', depth * 2);
            commentsList.Add($"{indent}Comment by {comment.Author}:\n{indent}{comment.BodyHtml}");

            if (comment.Replies?.Any() == true)
            {
                foreach (var reply in comment.Replies)
                {
                    AddComment(reply, depth + 1);
                }
            }
        }

        foreach (var comment in article.Comments)
        {
            AddComment(comment);
        }

        return string.Join("\n\n", commentsList);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing DevBlogs feedback...");
        await Task.Delay(1000); // Simulate analysis time

        var article = additionalData as DevBlogsArticleModel;
        var totalComments = article?.Comments.Sum(c => 
            1 + (c.Replies?.Sum(r => 1 + (r.Replies?.Count ?? 0)) ?? 0)) ?? 0;

        var mockAnalysis = @$"# DevBlogs Article Feedback Analysis 📚

## Overview
Article: {article?.Title ?? "Unknown Article"}
Total Comments: {totalComments}
Engagement Level: High
Overall Sentiment: Very Positive

## Key Themes 🎯

### Integration Features
- Multi-platform support well-received
- Strong interest in cloud integrations
- Azure integration highlighted as valuable

### Technical Interest
- Performance considerations discussed
- Scalability questions raised
- Implementation details requested

### Documentation & Support
- Clear documentation appreciated
- Good response time to questions
- Detailed explanations provided

## User Categories 👥
1. Platform Developers
   - Interested in integration capabilities
   - Focus on technical implementation
2. Blazor Developers
   - Looking for specific Blazor use-cases
   - Positive about implementation clarity
3. Performance-Focused Users
   - Inquiring about scalability
   - Interested in benchmarks

## Action Items 📋
1. Share performance benchmarks
2. Consider documenting cloud platform expansion plans
3. Create Blazor-specific integration guides

## Recommendations 💡
- Publish follow-up content on performance metrics
- Highlight Blazor integration scenarios
- Document scaling strategies
- Consider cloud platform roadmap communication

### Future Engagement
- Monitor for additional platform requests
- Track performance-related questions
- Plan regular documentation updates";

        return (mockAnalysis, article);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze comments
        return await AnalyzeComments(comments, additionalData);
    }
}
