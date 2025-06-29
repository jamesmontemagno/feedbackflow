using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Services.Interfaces;
using SharedDump.Services.Mock;

namespace FeedbackFlow.Tests;

[TestClass]
public class ApiValidationTests
{
    private MockGitHubService _mockGitHubService = null!;
    private MockRedditService _mockRedditService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGitHubService = new MockGitHubService();
        _mockRedditService = new MockRedditService();
    }

    [TestMethod]
    public async Task CheckRepositoryValid_ValidRepository_ReturnsTrue()
    {
        // Arrange & Act
        var result = await _mockGitHubService.CheckRepositoryValid("microsoft", "vscode");

        // Assert
        Assert.IsTrue(result, "Valid repository should return true");
    }

    [TestMethod]
    public async Task CheckRepositoryValid_InvalidRepository_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _mockGitHubService.CheckRepositoryValid("nonexistent", "repository");

        // Assert
        Assert.IsFalse(result, "Invalid repository should return false");
    }

    [TestMethod]
    public async Task CheckRepositoryValid_EmptyOwner_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _mockGitHubService.CheckRepositoryValid("", "repository");

        // Assert
        Assert.IsFalse(result, "Empty owner should return false");
    }

    [TestMethod]
    public async Task CheckRepositoryValid_EmptyRepo_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _mockGitHubService.CheckRepositoryValid("owner", "");

        // Assert
        Assert.IsFalse(result, "Empty repository name should return false");
    }

    [TestMethod]
    public async Task CheckSubredditValid_ValidSubreddit_ReturnsTrue()
    {
        // Arrange & Act
        var result = await _mockRedditService.CheckSubredditValid("dotnet");

        // Assert
        Assert.IsTrue(result, "Valid subreddit should return true");
    }

    [TestMethod]
    public async Task CheckSubredditValid_InvalidSubreddit_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _mockRedditService.CheckSubredditValid("nonexistentsubreddit123456");

        // Assert
        Assert.IsFalse(result, "Invalid subreddit should return false");
    }

    [TestMethod]
    public async Task CheckSubredditValid_EmptySubreddit_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _mockRedditService.CheckSubredditValid("");

        // Assert
        Assert.IsFalse(result, "Empty subreddit should return false");
    }

    [TestMethod]
    public async Task CheckSubredditValid_ValidSubreddits_ReturnTrue()
    {
        // Arrange
        var validSubreddits = new[] { "dotnet", "programming", "webdev", "csharp", "github" };

        // Act & Assert
        foreach (var subreddit in validSubreddits)
        {
            var result = await _mockRedditService.CheckSubredditValid(subreddit);
            Assert.IsTrue(result, $"Valid subreddit '{subreddit}' should return true");
        }
    }

    [TestMethod]
    public async Task CheckSubredditValid_CaseInsensitive_ReturnsTrue()
    {
        // Arrange & Act
        var lowerResult = await _mockRedditService.CheckSubredditValid("dotnet");
        var upperResult = await _mockRedditService.CheckSubredditValid("DOTNET");
        var mixedResult = await _mockRedditService.CheckSubredditValid("DoTnEt");

        // Assert
        Assert.IsTrue(lowerResult, "Lowercase subreddit should return true");
        Assert.IsTrue(upperResult, "Uppercase subreddit should return true");
        Assert.IsTrue(mixedResult, "Mixed case subreddit should return true");
    }
}