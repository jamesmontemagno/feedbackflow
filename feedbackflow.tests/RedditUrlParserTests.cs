using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;
using System.Linq;
using System.Threading.Tasks;

namespace FeedbackFlow.Tests;

[TestClass]
public class RedditUrlParserTests
{
    [TestMethod]
    public void ParseUrl_ValidThreadUrl_ReturnsThreadId()
    {
        var url = "https://www.reddit.com/r/test/comments/abc123/thread-title/";
        var id = RedditUrlParser.ParseUrl(url);
        Assert.AreEqual("abc123", id);
    }

    [TestMethod]
    public async Task ParseUrl_ValidShortlinkUrl_ReturnsThreadId()
    {
        var url = "https://www.reddit.com/r/dotnet/s/RwaAQyhfSu";
        var id = await RedditUrlParser.GetShortlinkIdAsync(url);
        Assert.AreEqual("1k5tpyc", id);
    }

    [TestMethod]
    public void ParseUrl_InvalidUrl_ReturnsNull()
    {
        var url = "https://www.reddit.com/r/test/other/abc123/";
        var id = RedditUrlParser.ParseUrl(url);
        Assert.IsNull(id);
    }

    [TestMethod]
    public void ParseMultipleIds_MixedInput_ReturnsAllIds()
    {
        var input = "https://www.reddit.com/r/test/comments/abc123/, def456, https://www.reddit.com/r/test/comments/ghi789/";
        var ids = RedditUrlParser.ParseMultipleIds(input).ToList();
        CollectionAssert.AreEqual(new[] { "abc123", "def456", "ghi789" }, ids);
    }

    [TestMethod]
    public void ParseMultipleIds_EmptyOrNull_ReturnsEmpty()
    {
        Assert.IsFalse(RedditUrlParser.ParseMultipleIds("").Any());
        Assert.IsFalse(RedditUrlParser.ParseMultipleIds(null!).Any());
    }
}
