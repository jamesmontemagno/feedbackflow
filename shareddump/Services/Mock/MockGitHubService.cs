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
        
        // Return false for invalid inputs
        if (string.IsNullOrWhiteSpace(repoOwner) || string.IsNullOrWhiteSpace(repoName))
            return false;
        
        // Mock some known valid repositories
        var validRepos = new[]
        {
            ("microsoft", "vscode"),
            ("dotnet", "runtime"),
            ("dotnet", "aspnetcore"),
            ("dotnet", "maui"),
            ("github", "docs"),
            ("facebook", "react"),
            ("nodejs", "node"),
            ("angular", "angular"),
            ("vercel", "next.js"),
            ("vuejs", "vue")
        };
        
        return validRepos.Any(repo => 
            repo.Item1.Equals(repoOwner, StringComparison.OrdinalIgnoreCase) &&
            repo.Item2.Equals(repoName, StringComparison.OrdinalIgnoreCase));
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
                Number = 42,
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
                Number = 57,
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
                Number = 63,
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
                Number = 44,
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
                Number = 45,
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

    /// <summary>
    /// Gets the oldest important issues that have recent comment activity - mock implementation
    /// </summary>
    public async Task<List<GithubIssueSummary>> GetOldestImportantIssuesWithRecentActivityAsync(string repoOwner, string repoName, int recentDays = 7, int topCount = 3)
    {
        // Simulate network delay
        await Task.Delay(600);

        // Simulate old issues that have had recent comment activity
        var oldImportantIssues = new List<GithubIssueSummary>
        {
            new()
            {
                Id = "old-issue-123",
                Title = "Long-standing performance issue with large datasets",
                State = "open",
                CreatedAt = DateTime.UtcNow.AddDays(-180), // 6 months old
                Author = "poweruser",
                Labels = new[] { "performance", "bug", "needs-investigation" },
                CommentsCount = 15,
                ReactionsCount = 8,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/123"
            },
            new()
            {
                Id = "old-issue-89",
                Title = "API versioning strategy discussion",
                State = "open",
                CreatedAt = DateTime.UtcNow.AddDays(-120), // 4 months old
                Author = "architect",
                Labels = new[] { "api", "breaking-change", "discussion" },
                CommentsCount = 22,
                ReactionsCount = 12,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/89"
            },
            new()
            {
                Id = "old-issue-67",
                Title = "Security vulnerability in authentication flow",
                State = "open",
                CreatedAt = DateTime.UtcNow.AddDays(-90), // 3 months old
                Author = "security-researcher",
                Labels = new[] { "security", "critical", "authentication" },
                CommentsCount = 18,
                ReactionsCount = 25,
                Url = $"https://github.com/{repoOwner}/{repoName}/issues/67"
            }
        };

        return oldImportantIssues.Take(topCount).ToList();
    }

    /// <summary>
    /// Gets a specific GitHub issue with all its comments - mock implementation
    /// </summary>
    public async Task<GithubIssueModel?> GetIssueWithCommentsAsync(string repoOwner, string repoName, int issueNumber)
    {
        // Simulate network delay
        await Task.Delay(800);

        // Return null for invalid inputs
        if (string.IsNullOrWhiteSpace(repoOwner) || string.IsNullOrWhiteSpace(repoName) || issueNumber <= 0)
            return null;

        // Mock issue data
        var mockIssue = new GithubIssueModel
        {
            Id = $"issue_{issueNumber}",
            Number = issueNumber,
            Author = "contributor123",
            Title = $"Sample Issue #{issueNumber}: Integration problems with new API",
            Body = "We're experiencing issues when integrating with the new API endpoints. The authentication flow seems to be inconsistent and sometimes returns 401 errors even with valid tokens.",
            URL = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastUpdated = DateTime.UtcNow.AddHours(-2),
            Upvotes = 8,
            Labels = new[] { "bug", "api", "authentication" },
            Comments = new[]
            {
                new GithubCommentModel
                {
                    Id = $"comment_{issueNumber}_1",
                    Author = "maintainer",
                    Content = "Thanks for reporting this! Can you provide more details about your authentication setup?",
                    CreatedAt = DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}#issuecomment-1"
                },
                new GithubCommentModel
                {
                    Id = $"comment_{issueNumber}_2",
                    Author = "contributor123",
                    Content = "Sure! I'm using OAuth2 with the client credentials flow. Here's my configuration: [code block]",
                    CreatedAt = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}#issuecomment-2"
                },
                new GithubCommentModel
                {
                    Id = $"comment_{issueNumber}_3",
                    Author = "developer456",
                    Content = "I'm seeing the same issue. It seems to happen more frequently during peak hours.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/issues/{issueNumber}#issuecomment-3"
                }
            }
        };

        return mockIssue;
    }

    /// <summary>
    /// Gets a specific GitHub pull request with all its comments - mock implementation
    /// </summary>
    public async Task<GithubIssueModel?> GetPullRequestWithCommentsAsync(string repoOwner, string repoName, int pullNumber)
    {
        // Simulate network delay
        await Task.Delay(900);

        // Return null for invalid inputs
        if (string.IsNullOrWhiteSpace(repoOwner) || string.IsNullOrWhiteSpace(repoName) || pullNumber <= 0)
            return null;

        // Mock pull request data
        var mockPr = new GithubIssueModel
        {
            Id = $"pr_{pullNumber}",
            Number = pullNumber,
            Author = "developer789",
            Title = $"Pull Request #{pullNumber}: Fix authentication timeout issues",
            Body = "This PR addresses the authentication timeout issues reported in #123. Changes include:\n\n- Increased timeout values for OAuth flows\n- Added retry logic for transient failures\n- Improved error handling and logging",
            URL = $"https://github.com/{repoOwner}/{repoName}/pull/{pullNumber}",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            LastUpdated = DateTime.UtcNow.AddHours(-1),
            Upvotes = 5,
            Labels = new[] { "enhancement", "authentication", "bug-fix" },
            Comments = new[]
            {
                new GithubCommentModel
                {
                    Id = $"pr_comment_{pullNumber}_1",
                    Author = "reviewer1",
                    Content = "Great work! The retry logic looks solid. Can you add some unit tests for the timeout scenarios?",
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/pull/{pullNumber}#issuecomment-1"
                },
                new GithubCommentModel
                {
                    Id = $"pr_comment_{pullNumber}_2",
                    Author = "developer789",
                    Content = "Good point! I'll add tests for the timeout and retry scenarios. Should have them up in a few hours.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/pull/{pullNumber}#issuecomment-2"
                },
                new GithubCommentModel
                {
                    Id = $"pr_review_comment_{pullNumber}_1",
                    Author = "senior-dev",
                    Content = "Consider using exponential backoff for the retry logic instead of fixed delays.",
                    CreatedAt = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/pull/{pullNumber}#discussion_r789012345",
                    FilePath = "src/Auth/AuthService.cs",
                    LinePosition = 45,
                    CodeContext = "@@ -42,6 +42,12 @@ public async Task<AuthResult> AuthenticateAsync()\n+            await Task.Delay(1000); // Fixed delay\n+            return await RetryAuthenticationAsync();"
                }
            }
        };

        return mockPr;
    }

    /// <summary>
    /// Gets a specific GitHub discussion with all its comments - mock implementation
    /// </summary>
    public async Task<GithubDiscussionModel?> GetDiscussionWithCommentsAsync(string repoOwner, string repoName, int discussionNumber)
    {
        // Simulate network delay
        await Task.Delay(700);

        // Return null for invalid inputs
        if (string.IsNullOrWhiteSpace(repoOwner) || string.IsNullOrWhiteSpace(repoName) || discussionNumber <= 0)
            return null;

        // Mock discussion data
        var mockDiscussion = new GithubDiscussionModel
        {
            Title = $"Discussion #{discussionNumber}: Best practices for API versioning",
            AnswerId = $"answer_{discussionNumber}_2", // Second comment is the accepted answer
            Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}",
            Comments = new[]
            {
                new GithubCommentModel
                {
                    Id = $"discussion_comment_{discussionNumber}_1",
                    Author = "community-member",
                    Content = "What's the recommended approach for versioning APIs in this project? Should we use URL versioning, header versioning, or something else?",
                    CreatedAt = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}#discussioncomment-1"
                },
                new GithubCommentModel
                {
                    Id = $"answer_{discussionNumber}_2",
                    Author = "maintainer",
                    Content = "We recommend using semantic versioning in the URL path (e.g., `/api/v1/`, `/api/v2/`). This makes it clear which version clients are using and allows for easier deprecation of old versions. Here's our versioning strategy:\n\n1. Major versions for breaking changes\n2. Minor versions for new features\n3. Patch versions for bug fixes\n\nWe maintain backward compatibility within major versions.",
                    CreatedAt = DateTime.UtcNow.AddDays(-6).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}#discussioncomment-2"
                },
                new GithubCommentModel
                {
                    Id = $"discussion_comment_{discussionNumber}_3",
                    Author = "api-expert",
                    Content = "Great answer! I'd also recommend using deprecation headers to warn clients about upcoming changes.",
                    CreatedAt = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}#discussioncomment-3",
                    ParentId = $"answer_{discussionNumber}_2"
                },
                new GithubCommentModel
                {
                    Id = $"discussion_comment_{discussionNumber}_4",
                    Author = "community-member",
                    Content = "Thanks for the detailed explanation! This is exactly what I was looking for.",
                    CreatedAt = DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Url = $"https://github.com/{repoOwner}/{repoName}/discussions/{discussionNumber}#discussioncomment-4",
                    ParentId = $"answer_{discussionNumber}_2"
                }
            }
        };

        return mockDiscussion;
    }
}
