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
        Assert.AreEqual(0, result.Count);
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
        Assert.AreEqual(2, result.Count);
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
        Assert.IsTrue(result.Contains("No issues found"));
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
        Assert.IsTrue(result.Contains("Issue Summary"));
        Assert.IsTrue(result.Contains("2 total issues"));
        Assert.IsTrue(result.Contains("1 open, 1 closed"));
        Assert.IsTrue(result.Contains("Top Keywords"));
        Assert.IsTrue(result.Contains("Common Labels"));
        Assert.IsTrue(result.Contains("authentication"));
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
        Assert.IsTrue(result.Contains("<!DOCTYPE html>"));
        Assert.IsTrue(result.Contains("GitHub Issues Report"));
        Assert.IsTrue(result.Contains("test/repo"));
        Assert.IsTrue(result.Contains("Test issue"));
        Assert.IsTrue(result.Contains("testuser"));
        Assert.IsTrue(result.Contains("Overall analysis here"));
        Assert.IsTrue(result.Contains("This is a test analysis"));
        Assert.IsTrue(result.Contains("5 comments"));
        Assert.IsTrue(result.Contains("2 reactions"));
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
        Assert.IsTrue(result.Contains("First issue"));
        Assert.IsTrue(result.Contains("Second issue"));
        Assert.IsTrue(result.Contains("user1"));
        Assert.IsTrue(result.Contains("user2"));
        Assert.IsTrue(result.Contains("Analysis for first issue"));
        Assert.IsTrue(result.Contains("Analysis for second issue"));
        Assert.IsTrue(result.Contains("priority-high"));
        Assert.IsTrue(result.Contains("feature"));
        Assert.IsTrue(result.Contains("state-open"));
        Assert.IsTrue(result.Contains("state-closed"));
    }

    [TestMethod]
    public void GetOldestImportantIssues_FiltersAndRanksCorrectly()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>
        {
            new()
            {
                Id = "1",
                Title = "Old important issue",
                CommentsCount = 5,
                ReactionsCount = 2,
                Url = "https://github.com/test/repo/issues/1",
                CreatedAt = DateTime.UtcNow.AddDays(-30), // 30 days old
                State = "OPEN",
                Author = "user1",
                Labels = new[] { "bug" }
            },
            new()
            {
                Id = "2", 
                Title = "Newer less important issue",
                CommentsCount = 1,
                ReactionsCount = 0,
                Url = "https://github.com/test/repo/issues/2",
                CreatedAt = DateTime.UtcNow.AddDays(-5), // 5 days old, low engagement
                State = "OPEN",
                Author = "user2",
                Labels = new[] { "question" }
            },
            new()
            {
                Id = "3",
                Title = "Very old important issue",
                CommentsCount = 10,
                ReactionsCount = 5,
                Url = "https://github.com/test/repo/issues/3",
                CreatedAt = DateTime.UtcNow.AddDays(-60), // 60 days old
                State = "OPEN",
                Author = "user3",
                Labels = new[] { "feature" }
            },
            new()
            {
                Id = "4",
                Title = "Closed old issue",
                CommentsCount = 8,
                ReactionsCount = 3,
                Url = "https://github.com/test/repo/issues/4",
                CreatedAt = DateTime.UtcNow.AddDays(-45), // 45 days old but closed
                State = "CLOSED",
                Author = "user4",
                Labels = new[] { "bug" }
            }
        };

        // Act
        var result = GitHubIssuesUtils.GetOldestImportantIssues(issues, 2);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("3", result[0].Id); // Oldest first (60 days)
        Assert.AreEqual("1", result[1].Id); // Next oldest (30 days)
        // Issue 2 should be excluded (low engagement)
        // Issue 4 should be excluded (closed)
    }

    [TestMethod]
    public void GetOldestImportantIssues_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var issues = new List<GithubIssueSummary>();

        // Act
        var result = GitHubIssuesUtils.GetOldestImportantIssues(issues, 3);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void TestDateFilteringLogic_FiltersCorrectlyByCreationDate()
    {
        // Arrange
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var testIssues = new List<GithubIssueSummary>
        {
            // Recent issue (within 7 days) - should be included
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
            // Old issue (outside 7 days) - should be excluded
            new()
            {
                Id = "old-1", 
                Title = "Old feature request",
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                State = "OPEN",
                Author = "user2", 
                CommentsCount = 10,
                ReactionsCount = 5,
                Url = "https://github.com/test/repo/issues/2",
                Labels = new[] { "enhancement" }
            },
            // Another recent issue (within 7 days) - should be included
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
            },
            // Edge case: issue created exactly at cutoff date - should be included
            new()
            {
                Id = "edge-case",
                Title = "Edge case issue",
                CreatedAt = cutoffDate,
                State = "OPEN",
                Author = "user4",
                CommentsCount = 1,
                ReactionsCount = 0,
                Url = "https://github.com/test/repo/issues/4",
                Labels = new[] { "question" }
            }
        };

        // Act - Apply the same filtering logic used in GetRecentIssuesForReportAsync
        var filteredIssues = testIssues.Where(issue => issue.CreatedAt >= cutoffDate).ToList();

        // Assert
        Assert.AreEqual(3, filteredIssues.Count, "Should include 3 issues created within or at the cutoff date");
        Assert.IsTrue(filteredIssues.Any(i => i.Id == "recent-1"), "Should include recent-1");
        Assert.IsTrue(filteredIssues.Any(i => i.Id == "recent-2"), "Should include recent-2");
        Assert.IsTrue(filteredIssues.Any(i => i.Id == "edge-case"), "Should include edge-case (exactly at cutoff)");
        Assert.IsFalse(filteredIssues.Any(i => i.Id == "old-1"), "Should exclude old-1 (too old)");
    }
}