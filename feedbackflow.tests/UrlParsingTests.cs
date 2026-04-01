using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class UrlParsingTests
{
    [TestMethod]
    public void ExtractVideoId_YouTubeWatchUrl_ReturnsId()
    {
        var input = "https://www.youtube.com/watch?v=hM4ifrqF_lQ";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractVideoId_YoutuBeShortUrl_ReturnsId()
    {
        var input = "https://youtu.be/hM4ifrqF_lQ";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractVideoId_YouTubeLiveUrl_ReturnsId()
    {
        var input = "https://www.youtube.com/live/hM4ifrqF_lQ";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractVideoId_YouTubeLiveUrlWithParameters_ReturnsId()
    {
        var input = "https://www.youtube.com/live/hM4ifrqF_lQ?si=SQQd4fmw4SzXAs4u";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractVideoId_RawId_ReturnsId()
    {
        var input = "hM4ifrqF_lQ";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractPlaylistId_PlaylistUrl_ReturnsId()
    {
        var input = "https://www.youtube.com/playlist?list=PL12345";
        var result = UrlParsing.ExtractPlaylistId(input);
        Assert.AreEqual("PL12345", result);
    }

    [TestMethod]
    public void ExtractPlaylistId_RawId_ReturnsId()
    {
        var input = "PL67890";
        var result = UrlParsing.ExtractPlaylistId(input);
        Assert.AreEqual("PL67890", result);
    }

    [TestMethod]
    public void ExtractHackerNewsId_HackerNewsUrl_ReturnsId()
    {
        var input = "https://news.ycombinator.com/item?id=123456";
        var result = UrlParsing.ExtractHackerNewsId(input);
        Assert.AreEqual("123456", result);
    }

    [TestMethod]
    public void ExtractHackerNewsId_RawId_ReturnsId()
    {
        var input = "789012";
        var result = UrlParsing.ExtractHackerNewsId(input);
        Assert.AreEqual("789012", result);
    }

    [TestMethod]
    public void ExtractVideoId_EmptyOrNull_ReturnsNull()
    {
        Assert.IsNull(UrlParsing.ExtractVideoId(""));
        Assert.IsNull(UrlParsing.ExtractVideoId(null!));
    }

    [TestMethod]
    public void ExtractRedditId_ValidThreadUrl_ReturnsId()
    {
        var result = UrlParsing.ExtractRedditId("https://www.reddit.com/r/dotnet/comments/abc123/some-title/");
        Assert.AreEqual("abc123", result);
    }

    [TestMethod]
    public void ExtractRedditId_DirectId_ReturnsId()
    {
        var result = UrlParsing.ExtractRedditId("abc123");
        Assert.AreEqual("abc123", result);
    }

    [TestMethod]
    public void ExtractRedditId_T3Prefix_ReturnsId()
    {
        var result = UrlParsing.ExtractRedditId("t3_abc123");
        Assert.AreEqual("abc123", result);
    }

    [TestMethod]
    public void ExtractRedditId_ShareUrl_ReturnsNull()
    {
        var result = UrlParsing.ExtractRedditId("https://www.reddit.com/r/dotnet/s/nInjTaac2X");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ExtractRedditId_ShareUrlWithHttp_ReturnsNull()
    {
        var result = UrlParsing.ExtractRedditId("http://www.reddit.com/r/csharp/s/abcDEF1234");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void IsRedditShareUrl_ValidShareUrl_ReturnsTrue()
    {
        Assert.IsTrue(UrlParsing.IsRedditShareUrl("https://www.reddit.com/r/dotnet/s/nInjTaac2X"));
    }

    [TestMethod]
    public void IsRedditShareUrl_HttpShareUrl_ReturnsTrue()
    {
        Assert.IsTrue(UrlParsing.IsRedditShareUrl("http://www.reddit.com/r/csharp/s/abcDEF1234"));
    }

    [TestMethod]
    public void IsRedditShareUrl_RegularThreadUrl_ReturnsFalse()
    {
        Assert.IsFalse(UrlParsing.IsRedditShareUrl("https://www.reddit.com/r/dotnet/comments/abc123/some-title/"));
    }

    [TestMethod]
    public void IsRedditShareUrl_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(UrlParsing.IsRedditShareUrl(""));
        Assert.IsFalse(UrlParsing.IsRedditShareUrl(null!));
    }
}
