using Microsoft.VisualStudio.TestTools.UnitTesting;
using FeedbackWebApp.Components.Feedback;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubUrlParserTests
{
    [TestMethod]
    public void ParseUrl_IssueUrl_ReturnsCorrectParts()
    {
        var url = "https://github.com/owner/repo/issues/123";
        var result = GitHubUrlParser.ParseUrl(url);
        Assert.IsNotNull(result);
        Assert.AreEqual(("owner", "repo", "issue", 123), result.Value);
    }

    [TestMethod]
    public void ParseUrl_PullRequestUrl_ReturnsCorrectParts()
    {
        var url = "https://github.com/owner/repo/pull/456";
        var result = GitHubUrlParser.ParseUrl(url);
        Assert.IsNotNull(result);
        Assert.AreEqual(("owner", "repo", "pull", 456), result.Value);
    }

    [TestMethod]
    public void ParseUrl_DiscussionUrl_ReturnsCorrectParts()
    {
        var url = "https://github.com/owner/repo/discussions/789";
        var result = GitHubUrlParser.ParseUrl(url);
        Assert.IsNotNull(result);
        Assert.AreEqual(("owner", "repo", "discussion", 789), result.Value);
    }

    [TestMethod]
    public void ParseUrl_InvalidUrl_ReturnsNull()
    {
        var url = "https://github.com/owner/repo/unknown/123";
        var result = GitHubUrlParser.ParseUrl(url);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseUrl_EmptyOrNull_ReturnsNull()
    {
        Assert.IsNull(GitHubUrlParser.ParseUrl(""));
        Assert.IsNull(GitHubUrlParser.ParseUrl(null!));
    }
}
