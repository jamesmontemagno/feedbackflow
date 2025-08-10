using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FeedbackFlow.Tests;

[TestClass]
public class ReanalyzeFeatureTests
{
    [TestMethod]
    public void NormalizeUrls_EmptyList_ReturnsEmpty()
    {
        // This tests the URL normalization functionality concept
        var urls = new List<string>();
        var result = NormalizeUrlsTestHelper(urls);
        
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public void NormalizeUrls_WithDuplicatesAndCasing_ReturnsNormalizedUnique()
    {
        var urls = new List<string> 
        { 
            "https://GITHUB.com/test/repo", 
            "https://github.com/test/repo",
            " https://github.com/test/repo ",
            "https://youtube.com/watch?v=123"
        };
        
        var result = NormalizeUrlsTestHelper(urls);
        
        Assert.AreEqual(2, result.Length);
        Assert.IsTrue(result.Any(u => u.Equals("https://github.com/test/repo", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Any(u => u.Equals("https://youtube.com/watch?v=123", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void NormalizeUrls_EmptyAndWhitespaceUrls_FiltersOut()
    {
        var urls = new List<string> 
        { 
            "https://github.com/test/repo", 
            "",
            "   ",
            null!,
            "https://youtube.com/watch?v=123"
        };
        
        var result = NormalizeUrlsTestHelper(urls);
        
        Assert.AreEqual(2, result.Length);
    }

    [TestMethod]
    public void UrlsUnchanged_SameUrls_ReturnsTrue()
    {
        var urls1 = new[] { "https://github.com/test/repo", "https://youtube.com/watch?v=123" };
        var urls2 = new[] { "https://GITHUB.com/test/repo", "https://youtube.com/watch?v=123" };
        
        var normalized1 = NormalizeUrlsTestHelper(urls1);
        var normalized2 = NormalizeUrlsTestHelper(urls2);
        
        Assert.IsTrue(normalized1.SequenceEqual(normalized2));
    }

    [TestMethod]
    public void UrlsUnchanged_DifferentUrls_ReturnsFalse()
    {
        var urls1 = new[] { "https://github.com/test/repo" };
        var urls2 = new[] { "https://github.com/different/repo" };
        
        var normalized1 = NormalizeUrlsTestHelper(urls1);
        var normalized2 = NormalizeUrlsTestHelper(urls2);
        
        Assert.IsFalse(normalized1.SequenceEqual(normalized2));
    }

    [TestMethod]
    public void UrlsUnchanged_DifferentOrder_ReturnsTrue()
    {
        var urls1 = new[] { "https://youtube.com/watch?v=123", "https://github.com/test/repo" };
        var urls2 = new[] { "https://github.com/test/repo", "https://youtube.com/watch?v=123" };
        
        var normalized1 = NormalizeUrlsTestHelper(urls1);
        var normalized2 = NormalizeUrlsTestHelper(urls2);
        
        Assert.IsTrue(normalized1.SequenceEqual(normalized2));
    }

    // Helper method that replicates the URL normalization logic from Home.razor
    private static string[] NormalizeUrlsTestHelper(IEnumerable<string> urls) => urls
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .Select(u => u.Trim().ToLowerInvariant()) // Normalize casing
        .Distinct()
        .OrderBy(u => u)
        .ToArray();
}