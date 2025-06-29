using SharedDump.Services.Mock;

namespace FeedbackFlow.Tests;

[TestClass]
public class RedditSubredditInfoTests
{
    [TestMethod]
    public async Task GetSubredditInfo_ValidSubreddit_ReturnsInfo()
    {
        // Arrange
        var mockRedditService = new MockRedditService();
        
        // Act
        var subredditInfo = await mockRedditService.GetSubredditInfo("dotnet");
        
        // Assert
        Assert.IsNotNull(subredditInfo);
        Assert.AreEqual("dotnet", subredditInfo.DisplayName);
        Assert.AreEqual("r/dotnet", subredditInfo.Title);
        Assert.IsTrue(subredditInfo.Subscribers > 0);
        Assert.IsTrue(subredditInfo.AccountsActive > 0);
        Assert.IsFalse(subredditInfo.Over18);
        Assert.AreEqual("public", subredditInfo.SubredditType);
        Assert.IsNotNull(subredditInfo.PublicDescription);
        Assert.IsNotNull(subredditInfo.Description);
    }

    [TestMethod]
    public async Task GetSubredditInfo_DifferentSubreddits_ReturnsDifferentStats()
    {
        // Arrange
        var mockRedditService = new MockRedditService();
        
        // Act
        var dotnetInfo = await mockRedditService.GetSubredditInfo("dotnet");
        var programmingInfo = await mockRedditService.GetSubredditInfo("programming");
        
        // Assert
        Assert.AreNotEqual(dotnetInfo.Subscribers, programmingInfo.Subscribers);
        Assert.AreNotEqual(dotnetInfo.AccountsActive, programmingInfo.AccountsActive);
        Assert.AreEqual("dotnet", dotnetInfo.DisplayName);
        Assert.AreEqual("programming", programmingInfo.DisplayName);
    }

    [TestMethod]
    public async Task GetSubredditInfo_UnknownSubreddit_ReturnsDefaultValues()
    {
        // Arrange
        var mockRedditService = new MockRedditService();
        
        // Act
        var subredditInfo = await mockRedditService.GetSubredditInfo("unknownsubreddit");
        
        // Assert
        Assert.IsNotNull(subredditInfo);
        Assert.AreEqual("unknownsubreddit", subredditInfo.DisplayName);
        Assert.AreEqual(50000, subredditInfo.Subscribers); // Default value
        Assert.AreEqual(150, subredditInfo.AccountsActive); // Default value
    }
}