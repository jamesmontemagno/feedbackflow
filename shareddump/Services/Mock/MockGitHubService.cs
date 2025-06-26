using SharedDump.Models.GitHub;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of IGitHubService for testing and development.
/// Provides realistic GitHub data without requiring API access.
/// </summary>
public class MockGitHubService : IGitHubService
{
    public async Task<bool> CheckRepositoryValid(string repoOwner, string repoName)
    {
        // Simulate network delay
        await Task.Delay(100);
        
        // Always return true for mock service
        return true;
    }

    public async Task<List<GithubDiscussionModel>> GetDiscussionsAsync(string repoOwner, string repoName)
    {
        // Simulate network delay
        await Task.Delay(500);

        return new List<GithubDiscussionModel>
        {
            new()
            {
                Title = "Roadmap discussion for Q3 2025",
                Url = $"https://github.com/{repoOwner}/{repoName}/discussions/12",
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-d1",
                        Author = "projectManager",
                        Content = "Let's discuss our priorities for the next quarter. We need to balance new features with technical debt and user-reported issues.",
                        CreatedAt = DateTime.UtcNow.AddDays(-5).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/12#discussioncomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-d2",
                        Author = "techLead",
                        Content = "I think we should focus on performance improvements first. Our app has been getting slower with each release.",
                        CreatedAt = DateTime.UtcNow.AddDays(-4).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/12#discussioncomment-2"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-d3",
                        Author = "designLead",
                        Content = "We've also had a lot of feedback about the UI being confusing for new users. We should consider a UX refresh.",
                        CreatedAt = DateTime.UtcNow.AddDays(-3).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/12#discussioncomment-3"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-d4",
                        Author = "communityManager",
                        Content = "Based on our community feedback, the most requested features are: 1) Dark mode improvements, 2) Mobile responsiveness, 3) Export to PDF functionality",
                        CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/12#discussioncomment-4"
                    }
                }
            },
            new()
            {
                Title = "How to improve user experience?",
                Url = $"https://github.com/{repoOwner}/{repoName}/discussions/8",
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-d5",
                        Author = "userB",
                        Content = "I think the navigation could be simplified. Users often get confused by the current menu structure.",
                        CreatedAt = DateTime.UtcNow.AddDays(-7).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/8#discussioncomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-d6",
                        Author = "userC",
                        Content = "Agreed! Also, the loading times could be improved. Maybe we should consider lazy loading for some components.",
                        CreatedAt = DateTime.UtcNow.AddDays(-6).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/discussions/8#discussioncomment-2"
                    }
                }
            }
        };
    }

    public async Task<List<GithubIssueModel>> GetIssuesAsync(string repoOwner, string repoName, string[] labels)
    {
        // Simulate network delay
        await Task.Delay(750);

        return new List<GithubIssueModel>
        {
            new()
            {
                Id = "42",
                Title = "Fix dark mode contrast issues",
                Author = "userA",
                URL = $"https://github.com/{repoOwner}/{repoName}/issues/42",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                LastUpdated = DateTime.UtcNow.AddDays(-1),
                Body = "The dark mode has poor contrast ratio in several UI components, making it difficult for users with visual impairments to read content. This is especially noticeable in the statistics dashboard where light gray text is almost unreadable against the dark background.",
                Upvotes = 18,
                Labels = new[] { "bug", "accessibility", "ui", "high-priority" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-i1",
                        Author = "userB",
                        Content = "I can confirm this. The contrast ratio needs to be at least 4.5:1 for normal text according to WCAG 2.1 AA standards.",
                        CreatedAt = DateTime.UtcNow.AddDays(-9).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/issues/42#issuecomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-i2",
                        Author = "userC",
                        Content = "This is especially noticeable in the statistics dashboard. The light gray text is almost unreadable against the dark background.",
                        CreatedAt = DateTime.UtcNow.AddDays(-8).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/issues/42#issuecomment-2"
                    }
                }
            },
            new()
            {
                Id = "57",
                Title = "Add keyboard shortcuts for common actions",
                Author = "userD",
                URL = $"https://github.com/{repoOwner}/{repoName}/issues/57",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                LastUpdated = DateTime.UtcNow.AddHours(-6),
                Body = "It would improve productivity to have keyboard shortcuts for frequently used actions like saving, creating new items, and navigation. Could we follow VS Code's keyboard shortcut style for consistency?",
                Upvotes = 25,
                Labels = new[] { "enhancement", "ux", "feature-request" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-i3",
                        Author = "userE",
                        Content = "Great idea! Could we follow VS Code's keyboard shortcut style for consistency?",
                        CreatedAt = DateTime.UtcNow.AddDays(-6).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/issues/57#issuecomment-1"
                    },
                    new GithubCommentModel
                    {
                        Id = "comment-i4",
                        Author = "userF",
                        Content = "We should make these configurable too. Different users have different preferences.",
                        CreatedAt = DateTime.UtcNow.AddDays(-5).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/issues/57#issuecomment-2"
                    }
                }
            },
            new()
            {
                Id = "63",
                Title = "Export functionality for reports",
                Author = "userG",
                URL = $"https://github.com/{repoOwner}/{repoName}/issues/63",
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                LastUpdated = DateTime.UtcNow.AddHours(-12),
                Body = "It would be great to have the ability to export data in different formats (CSV, JSON, PDF). This would help with reporting and data analysis.",
                Upvotes = 32,
                Labels = new[] { "enhancement", "feature-request", "high-demand" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-i5",
                        Author = "userH",
                        Content = "Yes! CSV export would be especially useful for integration with external tools.",
                        CreatedAt = DateTime.UtcNow.AddDays(-3).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/issues/63#issuecomment-1"
                    }
                }
            }
        };
    }

    public async Task<List<GithubIssueModel>> GetPullRequestsAsync(string repoOwner, string repoName, string[] labels)
    {
        // Simulate network delay
        await Task.Delay(600);

        return new List<GithubIssueModel>
        {
            new()
            {
                Id = "pr-44",
                Title = "Fix accessibility issues in dark mode",
                Author = "mockDeveloper1",
                URL = $"https://github.com/{repoOwner}/{repoName}/pull/44",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                LastUpdated = DateTime.UtcNow.AddHours(-2),
                Body = "This PR addresses the dark mode contrast issues raised in issue #42. Updated CSS variables and component styles to meet WCAG 2.1 AA standards.",
                Upvotes = 8,
                Labels = new[] { "accessibility", "ui", "ready-for-review" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-pr1",
                        Author = "mockReviewer1",
                        Content = "Looks good! The contrast improvements are significant. Just a few minor suggestions in the inline comments.",
                        CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/pull/44#issuecomment-1"
                    }
                }
            },
            new()
            {
                Id = "pr-45",
                Title = "Implement keyboard shortcuts",
                Author = "mockDeveloper2",
                URL = $"https://github.com/{repoOwner}/{repoName}/pull/45",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                LastUpdated = DateTime.UtcNow.AddHours(-4),
                Body = "Implementation of configurable keyboard shortcuts as requested in issue #57. Follows VS Code conventions and includes user preferences.",
                Upvotes = 12,
                Labels = new[] { "enhancement", "ux", "in-progress" },
                Comments = new[]
                {
                    new GithubCommentModel
                    {
                        Id = "comment-pr2",
                        Author = "mockReviewer2",
                        Content = "Nice work on the configurable aspect! The VS Code style consistency is perfect.",
                        CreatedAt = DateTime.UtcNow.AddHours(-8).ToString("O"),
                        Url = $"https://github.com/{repoOwner}/{repoName}/pull/45#issuecomment-1"
                    }
                }
            }
        };
    }

    public async Task<List<GithubCommentModel>> GetIssueCommentsAsync(string repoOwner, string repoName, int issueNumber)
    {
        // Simulate network delay
        await Task.Delay(300);

        return new List<GithubCommentModel>
        {
            new()
            {
                Id = $"mock-issue-comment-{issueNumber}-1",
                Author = "mockCommenter1",
                Content = $"This is a mock comment for issue #{issueNumber}. Great point about the implementation approach!",
                CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("O"),
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}#issuecomment-1"
            },
            new()
            {
                Id = $"mock-issue-comment-{issueNumber}-2",
                Author = "mockCommenter2",
                Content = $"I've encountered a similar issue. Here's a potential workaround that might help with issue #{issueNumber}.",
                CreatedAt = DateTime.UtcNow.AddDays(-1).ToString("O"),
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}#issuecomment-2"
            }
        };
    }

    public async Task<List<GithubCommentModel>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullNumber)
    {
        // Simulate network delay
        await Task.Delay(300);

        return new List<GithubCommentModel>
        {
            new()
            {
                Id = $"mock-pr-comment-{pullNumber}-1",
                Author = "mockReviewer1",
                Content = $"Mock review comment for PR #{pullNumber}. The implementation looks solid, but consider adding unit tests.",
                CreatedAt = DateTime.UtcNow.AddHours(-12).ToString("O"),
                Url = $"https://github.com/{repoOwner}/{repoName}/pull/{pullNumber}#issuecomment-1"
            }
        };
    }

    public async Task<List<GithubCommentModel>> GetDiscussionCommentsAsync(string repoOwner, string repoName, int discussionNumber)
    {
        // Simulate network delay
        await Task.Delay(300);

        return new List<GithubCommentModel>
        {
            new()
            {
                Id = $"mock-discussion-comment-{discussionNumber}-1",
                Author = "mockParticipant1",
                Content = $"Great discussion topic #{discussionNumber}! I think we should consider the user feedback more carefully.",
                CreatedAt = DateTime.UtcNow.AddDays(-1).ToString("O"),
                Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}#discussioncomment-1"
            }
        };
    }

    public async Task<List<GithubIssueSummary>> GetRecentIssuesForReportAsync(string repoOwner, string repoName, int daysBack = 7)
    {
        // Simulate network delay
        await Task.Delay(400);

        // Simulate GitHub's search API behavior with created:> filter
        // Only return issues that would match the server-side filter
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
        
        // These issues simulate what GitHub's search API would return
        // when using "repo:owner/repo is:issue created:>YYYY-MM-DD"
        var recentIssues = new List<GithubIssueSummary>
        {
            new()
            {
                Id = "mock-issue-42",
                Title = "Fix dark mode contrast issues",
                State = "open",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Author = "userA",
                Labels = new[] { "bug", "accessibility", "high-priority" },
                CommentsCount = 3,
                ReactionsCount = 18,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/42"
            },
            new()
            {
                Id = "mock-issue-57",
                Title = "Add keyboard shortcuts for common actions",
                State = "open", 
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                Author = "userD",
                Labels = new[] { "enhancement", "ux", "feature-request" },
                CommentsCount = 5,
                ReactionsCount = 25,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/57"
            },
            new()
            {
                Id = "mock-issue-63",
                Title = "Export functionality for reports",
                State = "open",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Author = "userG",
                Labels = new[] { "enhancement", "feature-request", "high-demand" },
                CommentsCount = 7,
                ReactionsCount = 32,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/63"
            }
        };

        // Filter to only include issues that would match GitHub's search criteria
        // GitHub's "created:>YYYY-MM-DD" filter would exclude older issues server-side
        return recentIssues.Where(issue => issue.CreatedAt >= cutoffDate).ToList();
    }
}
