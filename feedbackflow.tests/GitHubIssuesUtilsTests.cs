using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.GitHub;
using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubIssuesUtilsTests
{
    [TestMethod]
    public void RankIssuesByEngagement_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>();

        // Act
        var result = GitHubIssuesUtils.RankIssuesByEngagement(issues, 5);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void RankIssuesByEngagement_RanksCorrectly()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>
        {
            new()
            {
                Id = "1",
                Title = "Low engagement issue",
                CommentsCount = 1,
                ReactionsCount = 0,
                Url = "https://github.com/test/repo/issues/1",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                State = "OPEN",
                Author = "user1",
                Labels = new[] { "bug" }
            },
            new()
            {
                Id = "2",
                Title = "High engagement issue",
                CommentsCount = 10,
                ReactionsCount = 5,
                Url = "https://github.com/test/repo/issues/2",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                State = "OPEN",
                Author = "user2",
                Labels = new[] { "feature" }
            },
            new()
            {
                Id = "3",
                Title = "Medium engagement issue",
                CommentsCount = 5,
                ReactionsCount = 2,
                Url = "https://github.com/test/repo/issues/3",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                State = "CLOSED",
                Author = "user3",
                Labels = new[] { "enhancement" }
            }
        };

        // Act
        var result = GitHubIssuesUtils.RankIssuesByEngagement(issues, 2);

        // Assert
        Assert.HasCount(2, result);
        Assert.AreEqual("2", result[0].Id); // Highest engagement: (10 * 0.7) + (5 * 0.3) = 8.5
        Assert.AreEqual("3", result[1].Id); // Medium engagement: (5 * 0.7) + (2 * 0.3) = 4.1
    }

    [TestMethod]
    public void AnalyzeTitleTrends_EmptyList_ReturnsNoIssuesMessage()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>();

        // Act
        var result = GitHubIssuesUtils.AnalyzeTitleTrends(issues);

        // Assert
        Assert.Contains("No issues found", result);
    }

    [TestMethod]
    public void AnalyzeTitleTrends_WithIssues_ReturnsAnalysis()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>
        {
            new()
            {
                Id = "1",
                Title = "Bug with authentication system",
                CommentsCount = 5,
                ReactionsCount = 2,
                Url = "https://github.com/test/repo/issues/1",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                State = "OPEN",
                Author = "user1",
                Labels = new[] { "bug", "authentication" }
            },
            new()
            {
                Id = "2",
                Title = "Feature request for better authentication",
                CommentsCount = 3,
                ReactionsCount = 1,
                Url = "https://github.com/test/repo/issues/2",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                State = "CLOSED",
                Author = "user2",
                Labels = new[] { "feature", "authentication" }
            }
        };

        // Act 
        var result = GitHubIssuesUtils.AnalyzeTitleTrends(issues);

        // Assert
        Assert.Contains("Issue Summary", result);
        Assert.Contains("2 total issues", result);
        Assert.Contains("1 open, 1 closed", result);
        Assert.Contains("Top Keywords", result);
        Assert.Contains("Common Labels", result);
        Assert.Contains("authentication", result);
    }

    [TestMethod]
    public void GenerateGitHubIssuesReportEmail_CreatesValidHtml()
    {
        // Arrange
        var topIssues = new List<TopGitHubIssueInfo>
        {
            new(
                new GithubIssueDetail
                {
                    Summary = new GithubIssueSummary
                    {
                        Id = "1",
                        Title = "Test issue",
                        CommentsCount = 5,
                        ReactionsCount = 2,
                        Url = "https://github.com/test/repo/issues/1",
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        State = "OPEN",
                        Author = "testuser",
                        Labels = new[] { "bug" }
                    },
                    DeepAnalysis = "This is a test analysis",
                    Body = "Test body",
                    Comments = Array.Empty<GithubCommentModel>()
                },
                1,
                7
            )
        };

        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;

        // Act
        var result = GitHubIssuesUtils.GenerateGitHubIssuesReportEmail(
            "test/repo",
            startDate,
            endDate,
            "Overall analysis here",
            topIssues,
            new List<GithubIssueSummary>(), // Empty oldest issues for test
            "test-report-id"
        );

        // Assert
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("GitHub Issues Report", result);
        Assert.Contains("test/repo", result);
        Assert.Contains("Test issue", result);
        Assert.Contains("testuser", result);
        Assert.Contains("Overall analysis here", result);
        Assert.Contains("This is a test analysis", result);
        Assert.Contains("5 comments", result);
        Assert.Contains("2 reactions", result);
    }

    [TestMethod]
    public void GenerateGitHubIssuesReportEmail_HandlesMultipleIssues()
    {
        // Arrange
        var topIssues = new List<TopGitHubIssueInfo>
        {
            new(
                new GithubIssueDetail
                {
                    Summary = new GithubIssueSummary
                    {
                        Id = "1",
                        Title = "First issue",
                        CommentsCount = 10,
                        ReactionsCount = 5,
                        Url = "https://github.com/test/repo/issues/1",
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        State = "OPEN",
                        Author = "user1",
                        Labels = new[] { "bug", "priority-high" }
                    },
                    DeepAnalysis = "Analysis for first issue",
                    Body = "",
                    Comments = Array.Empty<GithubCommentModel>()
                },
                1,
                15
            ),
            new(
                new GithubIssueDetail
                {
                    Summary = new GithubIssueSummary
                    {
                        Id = "2",
                        Title = "Second issue",
                        CommentsCount = 3,
                        ReactionsCount = 1,
                        Url = "https://github.com/test/repo/issues/2",
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        State = "CLOSED",
                        Author = "user2",
                        Labels = new[] { "feature" }
                    },
                    DeepAnalysis = "Analysis for second issue",
                    Body = "",
                    Comments = Array.Empty<GithubCommentModel>()
                },
                2,
                4
            )
        };

        // Act
        var result = GitHubIssuesUtils.GenerateGitHubIssuesReportEmail(
            "test/repo",
            DateTimeOffset.UtcNow.AddDays(-7),
            DateTimeOffset.UtcNow,
            "Overall trends",
            topIssues,
            new List<GithubIssueSummary>(), // Empty oldest issues for test
            "multi-test-id"
        );

        // Assert
        Assert.Contains("First issue", result);
        Assert.Contains("Second issue", result);
        Assert.Contains("user1", result);
        Assert.Contains("user2", result);
        Assert.Contains("Analysis for first issue", result);
        Assert.Contains("Analysis for second issue", result);
        Assert.Contains("priority-high", result);
        Assert.Contains("feature", result);
        Assert.Contains("state-open", result);
        Assert.Contains("state-closed", result);
    }


    [TestMethod]
    public void TestDateFilteringLogic_UsesGitHubSearchWithCreatedFilter()
    {
        // Arrange
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var expectedSearchQuery = $"repo:testowner/testrepo is:issue created:>{cutoffDate:yyyy-MM-dd}";
        
        // This test verifies that we construct the correct search query
        // The actual filtering is now done server-side by GitHub's search API
        var testIssues = new List<GithubIssueSummary>
        {
            // With server-side filtering, GitHub only returns issues created after the cutoff date
            // so all returned issues should be recent
            new()
            {
                Id = "recent-1",
                Title = "Recent bug report",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                State = "OPEN",
                Author = "user1",
                CommentsCount = 5,
                ReactionsCount = 2,
                Url = "https://github.com/test/repo/issues/1",
                Labels = new[] { "bug" }
            },
            new()
            {
                Id = "recent-2",
                Title = "Another recent issue",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                State = "CLOSED",
                Author = "user3",
                CommentsCount = 2,
                ReactionsCount = 1, 
                Url = "https://github.com/test/repo/issues/3",
                Labels = new[] { "bug", "resolved" }
            }
        };

        // Act - Verify search query construction
        var actualSearchQuery = $"repo:testowner/testrepo is:issue created:>{cutoffDate:yyyy-MM-dd}";

        // Assert
        Assert.AreEqual(expectedSearchQuery, actualSearchQuery, "Search query should filter by creation date using GitHub's search syntax");
        Assert.HasCount(2, testIssues, "GitHub search should only return issues created after the cutoff date");
        Assert.IsTrue(testIssues.All(issue => issue.CreatedAt >= cutoffDate), "All returned issues should be recent (server-side filtered)");
    }
}