using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.GitHub;

namespace FeedbackWebApp.Services.Mock;

public class MockGitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    public MockGitHubFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub feedback...");
        
        // Simulate network delay
        await Task.Delay(1500);

        // Create mock GitHub issues
        var issues = new List<GithubIssueModel>
        {
            new()
            {
                Id = "42",
                Title = "Fix dark mode contrast issues",
                Author = "userA",
                URL = "https://github.com/sample/repo/issues/42",
                CreatedAt = new DateTime(2025, 3, 12, 9, 15, 22, DateTimeKind.Utc),
                LastUpdated = new DateTime(2025, 4, 1, 14, 32, 15, DateTimeKind.Utc),
                Body = "The dark mode has poor contrast ratio in several UI components, making it difficult for users with visual impairments to read content.",
                Upvotes = 15,
                Labels = new[] { "bug", "accessibility", "ui" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-1",
                        Author = "userB",
                        Content = "I can confirm this. The contrast ratio needs to be at least 4.5:1 for normal text according to WCAG 2.1 AA standards.",
                        CreatedAt = "2025-03-12T11:23:45Z",
                        Url = "https://github.com/sample/repo/issues/42#issuecomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-2",
                        Author = "userC",
                        Content = "This is especially noticeable in the statistics dashboard. The light gray text is almost unreadable against the dark background.",
                        CreatedAt = "2025-03-14T08:17:33Z",
                        Url = "https://github.com/sample/repo/issues/42#issuecomment-2"
                    }
                }
            },
            new()
            {
                Id = "57",
                Title = "Add keyboard shortcuts for common actions",
                Author = "userD",
                URL = "https://github.com/sample/repo/issues/57",
                CreatedAt = new DateTime(2025, 3, 20, 15, 45, 12, DateTimeKind.Utc),
                LastUpdated = new DateTime(2025, 4, 5, 11, 9, 27, DateTimeKind.Utc),
                Body = "It would improve productivity to have keyboard shortcuts for frequently used actions like saving, creating new items, and navigation.",
                Upvotes = 8,
                Labels = new[] { "enhancement", "ux" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-3",
                        Author = "userE",
                        Content = "Great idea! Could we follow VS Code's keyboard shortcut style for consistency?",
                        CreatedAt = "2025-03-22T10:12:08Z",
                        Url = "https://github.com/sample/repo/issues/57#issuecomment-3"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-4",
                        Author = "userF",
                        Content = "We should make these configurable too. Different users have different preferences.",
                        CreatedAt = "2025-03-25T14:30:41Z",
                        Url = "https://github.com/sample/repo/issues/57#issuecomment-4"
                    }
                }
            }
        };

        // Create mock GitHub discussions
        var discussions = new List<GithubDiscussionModel>
        {
            new()
            {
                Title = "Roadmap discussion for Q3 2025",
                Url = "https://github.com/sample/repo/discussions/12",
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-5",
                        Author = "projectManager",
                        Content = "Let's discuss our priorities for the next quarter. We need to balance new features with technical debt and user-reported issues.",
                        CreatedAt = "2025-03-01T10:00:00Z",
                        Url = "https://github.com/sample/repo/discussions/12#discussioncomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-6",
                        Author = "techLead",
                        Content = "I think we should focus on performance improvements first. Our app has been getting slower with each release.",
                        CreatedAt = "2025-03-01T13:45:22Z",
                        Url = "https://github.com/sample/repo/discussions/12#discussioncomment-2"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-7",
                        Author = "designLead",
                        Content = "We've also had a lot of feedback about the UI being confusing for new users. We should consider a UX refresh.",
                        CreatedAt = "2025-03-02T09:12:33Z",
                        Url = "https://github.com/sample/repo/discussions/12#discussioncomment-3"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-8",
                        Author = "communityManager",
                        Content = "Based on our community feedback, the most requested features are: 1) Dark mode improvements, 2) Mobile responsiveness, 3) Export to PDF functionality",
                        CreatedAt = "2025-03-03T11:07:18Z",
                        Url = "https://github.com/sample/repo/discussions/12#discussioncomment-4"
                    }
                }
            }
        };

        // Build response data object
        var responseData = new { Issues = issues, Discussions = discussions };

        // Build the comments string
        var allComments = string.Join("\n\n", issues.Select(issue =>
        {
            var comments = new List<string>
            {
                $"Issue: {issue.Title}",
                $"Description: {issue.Body}"
            };

            if (issue.Comments != null)
            {
                comments.AddRange(issue.Comments.Select(comment =>
                    $"Comment by {comment.Author}: {comment.Content}"
                ));
            }

            return string.Join("\n", comments);
        }));

        allComments += "\n\n" + string.Join("\n\n", discussions.Select(discussion =>
        {
            var comments = new List<string>
            {
                $"Discussion: {discussion.Title}"
            };

            if (discussion.Comments != null)
            {
                comments.AddRange(discussion.Comments.Select(comment =>
                    $"Comment by {comment.Author}: {comment.Content}"
                ));
            }

            return string.Join("\n", comments);
        }));

        // Count total comments (issue comments + discussion comments + issues descriptions + discussions)
        int totalComments = 
            issues.Sum(i => (i.Comments?.Length ?? 0) + 1) + // Issue comments + descriptions
            discussions.Sum(d => d.Comments?.Length ?? 0); // Discussion comments

        return (allComments, totalComments, responseData);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing GitHub feedback...");
        await Task.Delay(2000);

        // Use provided comment count or calculate
        int totalComments = commentCount ?? comments.Split('\n').Count(line => line.StartsWith("Comment by"));

        var mockAnalysis = @$"# GitHub Feedback Analysis

## Overview
Based on {totalComments} GitHub issues, comments, and discussions, there are several key areas of focus:

1. **Accessibility Improvements** - Particularly for dark mode
2. **User Experience Enhancements** - Including keyboard shortcuts and UI clarity
3. **Performance Optimization** - App speed degrading with releases

## Key Insights

### Accessibility
- Critical dark mode contrast issues affecting readability ({totalComments} total items of feedback)
- Multiple users reporting WCAG compliance concerns
- Statistics dashboard particularly problematic

### User Experience
- Keyboard shortcuts highly requested
- Preference for VS Code-like shortcut system
- Need for customizable shortcuts
- UI confusion reported by new users

### Performance and Technical
- Application performance degrading with updates
- Need to balance new features with technical debt
- Mobile responsiveness improvements needed

### Feature Requests
1. Dark mode improvements
2. Mobile responsive design
3. PDF export functionality
4. Keyboard shortcuts
5. Configurable UI elements

## Community Engagement
- Active discussion participation ({totalComments} contributions)
- Detailed, constructive feedback
- Strong interest in accessibility features

## Sentiment Analysis
Overall sentiment is constructive rather than critical. Users are engaged and providing specific, actionable feedback.";

        return (mockAnalysis, additionalData);
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