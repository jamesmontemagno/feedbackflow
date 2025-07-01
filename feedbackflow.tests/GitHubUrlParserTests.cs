using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubUrlParserTests
{
    [TestMethod]
    public void ParseGitHubUrl_ValidIssueUrl_ReturnsCorrectInfo()
    {
        // Arrange
        var url = "https://github.com/dotnet/maui/issues/123";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("dotnet", result.Owner);
        Assert.AreEqual("maui", result.Repository);
        Assert.AreEqual(GitHubUrlType.Issue, result.Type);
        Assert.AreEqual(123, result.Number);
    }

    [TestMethod]
    public void ParseGitHubUrl_ValidPullRequestUrl_ReturnsCorrectInfo()
    {
        // Arrange
        var url = "https://github.com/microsoft/vscode/pull/456";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("microsoft", result.Owner);
        Assert.AreEqual("vscode", result.Repository);
        Assert.AreEqual(GitHubUrlType.PullRequest, result.Type);
        Assert.AreEqual(456, result.Number);
    }

    [TestMethod]
    public void ParseGitHubUrl_ValidDiscussionUrl_ReturnsCorrectInfo()
    {
        // Arrange
        var url = "https://github.com/jamesmontemagno/feedbackflow/discussions/789";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("jamesmontemagno", result.Owner);
        Assert.AreEqual("feedbackflow", result.Repository);
        Assert.AreEqual(GitHubUrlType.Discussion, result.Type);
        Assert.AreEqual(789, result.Number);
    }

    [TestMethod]
    public void ParseGitHubUrl_RepositoryUrl_ReturnsRepositoryInfo()
    {
        // Arrange
        var url = "https://github.com/dotnet/runtime";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("dotnet", result.Owner);
        Assert.AreEqual("runtime", result.Repository);
        Assert.AreEqual(GitHubUrlType.Repository, result.Type);
        Assert.IsNull(result.Number);
    }

    [TestMethod]
    public void ParseGitHubUrl_InvalidUrl_ReturnsNull()
    {
        // Arrange
        var url = "https://example.com/invalid";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseGitHubUrl_EmptyUrl_ReturnsNull()
    {
        // Arrange
        var url = "";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseGitHubUrl_NonNumericIssueNumber_ReturnsRepository()
    {
        // Arrange
        var url = "https://github.com/dotnet/maui/issues/abc";

        // Act
        var result = GitHubUrlParser.ParseGitHubUrl(url);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("dotnet", result.Owner);
        Assert.AreEqual("maui", result.Repository);
        Assert.AreEqual(GitHubUrlType.Repository, result.Type);
        Assert.IsNull(result.Number);
    }

    [TestMethod]
    public void IsGitHubUrl_ValidGitHubUrl_ReturnsTrue()
    {
        // Arrange
        var url = "https://github.com/dotnet/maui/issues/123";

        // Act
        var result = GitHubUrlParser.IsGitHubUrl(url);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsGitHubUrl_InvalidUrl_ReturnsFalse()
    {
        // Arrange
        var url = "https://example.com/invalid";

        // Act
        var result = GitHubUrlParser.IsGitHubUrl(url);

        // Assert
        Assert.IsFalse(result);
    }
}
