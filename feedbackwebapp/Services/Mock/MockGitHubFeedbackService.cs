using System.Text.Json;
using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.GitHub;

namespace FeedbackWebApp.Services.Mock;

public class MockGitHubFeedbackService : FeedbackService, IGitHubFeedbackService
{
    public MockGitHubFeedbackService(
        HttpClient http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching GitHub feedback...");
        
        // Simulate network delay
        await Task.Delay(1500);

        // Create mock GitHub issues
        var issues = new List<GithubIssueModel>
        {
            new GithubIssueModel
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
                        Content = "Specifically the sidebar navigation has gray text (#7a7a7a) on a dark background (#222) which is only about 3.6:1 ratio.",
                        CreatedAt = "2025-03-13T08:17:32Z",
                        Url = "https://github.com/sample/repo/issues/42#issuecomment-2"
                    }
                }
            },
            new GithubIssueModel
            {
                Id = "57",
                Title = "Add keyboard shortcuts for common actions",
                Author = "userD",
                URL = "https://github.com/sample/repo/issues/57",
                CreatedAt = new DateTime(2025, 3, 21, 16, 42, 11, DateTimeKind.Utc),
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
            new GithubDiscussionModel
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

        // Create a response object that matches the API's response structure
        var responseData = new
        {
            Issues = issues,
            PullRequests = new List<GithubIssueModel>(), // Empty pull requests list
            Discussions = discussions
        };

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing GitHub feedback...");
        await Task.Delay(2000);

        var mockAnalysis = @"# GitHub Feedback Analysis

## Overview
Based on the GitHub issues and discussions, there are several key areas of focus:

1. **Accessibility Improvements** - Particularly for dark mode
2. **User Experience Enhancements** - Including keyboard shortcuts and UI clarity
3. **Performance Optimization** - App speed degrading with releases

## Key Insights

### Accessibility
Users report contrast issues in dark mode, specifically in the navigation sidebar. The current contrast ratio (3.6:1) falls below WCAG 2.1 AA standards (4.5:1). This should be prioritized as it impacts users with visual impairments.

### User Experience
Multiple users have requested keyboard shortcuts for common actions. Suggestions include:
- Following VS Code's shortcut patterns for familiarity
- Making shortcuts configurable to accommodate different user preferences

### Roadmap Priorities
Based on community feedback, the top three requested features are:
1. Dark mode improvements
2. Mobile responsiveness
3. Export to PDF functionality

## Recommendations
1. Address the contrast issues in dark mode as a priority fix
2. Implement keyboard shortcuts with configuration options
3. Consider a focused UX review to identify confusing elements for new users
4. Include performance optimization in the technical backlog

## Sentiment Analysis
Overall sentiment is constructive rather than critical. Users are engaged and providing specific, actionable feedback.";

        return (mockAnalysis, responseData);
    }
}