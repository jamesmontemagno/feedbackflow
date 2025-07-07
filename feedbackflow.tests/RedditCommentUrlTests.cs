using SharedDump.Models.Reddit;
using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class RedditCommentUrlTests
{
    [TestMethod]
    public void GenerateCommentUrl_WithValidPermalinkAndCommentId_ReturnsCorrectUrl()
    {
        // Arrange
        var parentThread = new RedditThreadModel
        {
            Id = "abc123",
            Title = "Test Thread",
            Author = "testuser",
            Url = "https://reddit.com/r/test/comments/abc123/test_thread/",
            Permalink = "/r/test/comments/abc123/test_thread/"
        };
        
        var comment = new RedditCommentModel
        {
            Id = "def456", 
            Author = "commenter",
            Body = "Test comment"
        };

        // Act - this is the current broken logic
        var currentBrokenUrl = $"https://reddit.com{parentThread.Permalink}{comment.Id}";
        
        // Assert - show what we currently get (broken)
        Assert.AreEqual("https://reddit.com/r/test/comments/abc123/test_thread/def456", currentBrokenUrl);
        
        // Test the fixed logic using the new utility method
        var fixedUrl = RedditUrlParser.GenerateCommentUrl(parentThread.Permalink, comment.Id);
        
        // This is what a properly formed Reddit comment URL should look like
        var expectedUrl = "https://www.reddit.com/r/test/comments/abc123/test_thread/def456/";
        Assert.AreEqual(expectedUrl, fixedUrl);
    }

    [TestMethod]
    public void GenerateCommentUrl_WithPermalinkMissingLeadingSlash_HandlesCorrectly()
    {
        // Arrange
        var parentThread = new RedditThreadModel
        {
            Id = "abc123",
            Title = "Test Thread", 
            Author = "testuser",
            Url = "https://reddit.com/r/test/comments/abc123/test_thread/",
            Permalink = "r/test/comments/abc123/test_thread/" // Missing leading slash
        };
        
        var comment = new RedditCommentModel
        {
            Id = "def456",
            Author = "commenter", 
            Body = "Test comment"
        };

        // Act - test the fixed logic we'll implement
        var fixedUrl = RedditUrlParser.GenerateCommentUrl(parentThread.Permalink, comment.Id);
        
        // Assert
        Assert.AreEqual("https://www.reddit.com/r/test/comments/abc123/test_thread/def456/", fixedUrl);
    }

    [TestMethod]
    public void GenerateCommentUrl_WithPermalinkEndingSlash_HandlesCorrectly()
    {
        // Arrange 
        var parentThread = new RedditThreadModel
        {
            Id = "abc123",
            Title = "Test Thread",
            Author = "testuser", 
            Url = "https://reddit.com/r/test/comments/abc123/test_thread/",
            Permalink = "/r/test/comments/abc123/test_thread/" // Has trailing slash
        };
        
        var comment = new RedditCommentModel
        {
            Id = "def456",
            Author = "commenter",
            Body = "Test comment"
        };

        // Act
        var fixedUrl = RedditUrlParser.GenerateCommentUrl(parentThread.Permalink, comment.Id);
        
        // Assert
        Assert.AreEqual("https://www.reddit.com/r/test/comments/abc123/test_thread/def456/", fixedUrl);
    }

    [TestMethod]  
    public void GenerateCommentUrl_WithPermalinkNoEndingSlash_HandlesCorrectly()
    {
        // Arrange
        var parentThread = new RedditThreadModel
        {
            Id = "abc123", 
            Title = "Test Thread",
            Author = "testuser",
            Url = "https://reddit.com/r/test/comments/abc123/test_thread",
            Permalink = "/r/test/comments/abc123/test_thread" // No trailing slash
        };
        
        var comment = new RedditCommentModel
        {
            Id = "def456",
            Author = "commenter",
            Body = "Test comment"
        };

        // Act
        var fixedUrl = RedditUrlParser.GenerateCommentUrl(parentThread.Permalink, comment.Id);
        
        // Assert
        Assert.AreEqual("https://www.reddit.com/r/test/comments/abc123/test_thread/def456/", fixedUrl);
    }

    [TestMethod]
    public void GenerateCommentUrl_WithEmptyInputs_ReturnsEmptyString()
    {
        // Act & Assert
        Assert.AreEqual("", RedditUrlParser.GenerateCommentUrl("", "def456"));
        Assert.AreEqual("", RedditUrlParser.GenerateCommentUrl("/r/test/comments/abc123/", ""));
        Assert.AreEqual("", RedditUrlParser.GenerateCommentUrl("", ""));
        Assert.AreEqual("", RedditUrlParser.GenerateCommentUrl(null!, "def456"));
        Assert.AreEqual("", RedditUrlParser.GenerateCommentUrl("/r/test/comments/abc123/", null!));
    }
}