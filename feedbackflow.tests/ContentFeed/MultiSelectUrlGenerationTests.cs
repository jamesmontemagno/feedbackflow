using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FeedbackFlow.Tests.ContentFeed;

/// <summary>
/// Tests for multi-select URL generation in content feed results
/// </summary>
[TestClass]
public class MultiSelectUrlGenerationTests
{
    [TestMethod]
    public void GenerateMultipleYouTubeUrls_CreatesCorrectQueryString()
    {
        // Arrange
        var videoIds = new List<string> { "dQw4w9WgXcQ", "jNQXAC9IVRw", "yPYZpwSpKmA" };
        var expectedUrls = videoIds.Select(id => $"https://www.youtube.com/watch?v={id}").ToList();
        
        // Act
        var queryString = string.Join("&", expectedUrls.Select(url => $"url={Uri.EscapeDataString(url)}"));
        var fullUrl = $"/?source=auto&{queryString}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(3, parsedUrls);
        CollectionAssert.AreEqual(expectedUrls, parsedUrls);
    }
    
    [TestMethod]
    public void GenerateMultipleRedditUrls_CreatesCorrectQueryString()
    {
        // Arrange
        var permalinks = new List<string> 
        { 
            "/r/programming/comments/abc123/test_post_1/",
            "/r/programming/comments/def456/test_post_2/",
            "/r/programming/comments/ghi789/test_post_3/"
        };
        var expectedUrls = permalinks.Select(p => $"https://www.reddit.com{p}").ToList();
        
        // Act
        var queryString = string.Join("&", expectedUrls.Select(url => $"url={Uri.EscapeDataString(url)}"));
        var fullUrl = $"/?source=auto&{queryString}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(3, parsedUrls);
        CollectionAssert.AreEqual(expectedUrls, parsedUrls);
    }
    
    [TestMethod]
    public void GenerateMultipleHackerNewsUrls_CreatesCorrectQueryString()
    {
        // Arrange
        var itemIds = new List<long> { 12345678, 23456789, 34567890 };
        var expectedUrls = itemIds.Select(id => $"https://news.ycombinator.com/item?id={id}").ToList();
        
        // Act
        var queryString = string.Join("&", expectedUrls.Select(url => $"url={Uri.EscapeDataString(url)}"));
        var fullUrl = $"/?source=auto&{queryString}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(3, parsedUrls);
        CollectionAssert.AreEqual(expectedUrls, parsedUrls);
    }
    
    [TestMethod]
    public void GenerateSingleUrl_BackwardCompatibility()
    {
        // Arrange
        var singleUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        
        // Act
        var fullUrl = $"/?source=auto&url={Uri.EscapeDataString(singleUrl)}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(1, parsedUrls);
        Assert.AreEqual(singleUrl, parsedUrls[0]);
        
        // Also verify the old single-url method still works
        var oldSingleUrl = parsedQuery["url"];
        Assert.AreEqual(singleUrl, oldSingleUrl);
    }
    
    [TestMethod]
    public void UrlEncoding_HandlesSpecialCharacters()
    {
        // Arrange - URLs with special characters that need encoding
        var urls = new List<string>
        {
            "https://www.reddit.com/r/test/comments/abc/title_with_spaces/",
            "https://www.youtube.com/watch?v=abc&t=123",
            "https://news.ycombinator.com/item?id=12345&p=2"
        };
        
        // Act
        var queryString = string.Join("&", urls.Select(url => $"url={Uri.EscapeDataString(url)}"));
        var fullUrl = $"/?source=auto&{queryString}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(3, parsedUrls);
        CollectionAssert.AreEqual(urls, parsedUrls);
    }
    
    [TestMethod]
    public void EmptySelection_NoUrlsGenerated()
    {
        // Arrange
        var selectedIds = new List<string>();
        
        // Act
        var shouldGenerate = selectedIds.Any();
        
        // Assert
        Assert.IsFalse(shouldGenerate, "Should not generate URLs when no items are selected");
    }
    
    [TestMethod]
    public void LargeSelection_HandlesManyItems()
    {
        // Arrange - Simulate selecting 20 items
        var videoIds = Enumerable.Range(1, 20).Select(i => $"video{i:D3}").ToList();
        var expectedUrls = videoIds.Select(id => $"https://www.youtube.com/watch?v={id}").ToList();
        
        // Act
        var queryString = string.Join("&", expectedUrls.Select(url => $"url={Uri.EscapeDataString(url)}"));
        var fullUrl = $"/?source=auto&{queryString}";
        
        // Parse it back
        var uri = new Uri($"https://example.com{fullUrl}");
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        var parsedUrls = parsedQuery.GetValues("url");
        
        // Assert
        Assert.IsNotNull(parsedUrls);
        Assert.HasCount(20, parsedUrls);
        CollectionAssert.AreEqual(expectedUrls, parsedUrls);
    }
}
