using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;
using System.Linq;

namespace FeedbackFlow.Tests;

[TestClass]
public class UrlParsingTests
{
    [TestMethod]
    public void ExtractVideoId_WatchAndShortAndLiveUrls_ReturnsId()
    {
        var input = "https://www.youtube.com/watch?v=hM4ifrqF_lQ, https://youtu.be/hM4ifrqF_lQ, https://www.youtube.com/live/hM4ifrqF_lQ, https://www.youtube.com/live/hM4ifrqF_lQ?si=SQQd4fmw4SzXAs4u";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ,hM4ifrqF_lQ,hM4ifrqF_lQ,hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractVideoId_RawId_ReturnsId()
    {
        var input = "hM4ifrqF_lQ";
        var result = UrlParsing.ExtractVideoId(input);
        Assert.AreEqual("hM4ifrqF_lQ", result);
    }

    [TestMethod]
    public void ExtractPlaylistId_UrlAndRaw_ReturnsId()
    {
        var input = "https://www.youtube.com/playlist?list=PL12345, PL67890";
        var result = UrlParsing.ExtractPlaylistId(input);
        Assert.AreEqual("PL12345,PL67890", result);
    }

    [TestMethod]
    public void ExtractHackerNewsId_UrlAndRaw_ReturnsId()
    {
        var input = "https://news.ycombinator.com/item?id=123456, 789012";
        var result = UrlParsing.ExtractHackerNewsId(input);
        Assert.AreEqual("123456,789012", result);
    }

    [TestMethod]
    public void ExtractVideoId_EmptyOrNull_ReturnsNull()
    {
        Assert.IsNull(UrlParsing.ExtractVideoId(""));
        Assert.IsNull(UrlParsing.ExtractVideoId(null!));
    }
}
