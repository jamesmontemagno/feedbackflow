using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class StringUtilitiesTests
{
    [TestMethod]
    public void Slugify_AlphaNumeric_ReturnsLowercase()
    {
        var input = "HelloWorld123";
        var result = StringUtilities.Slugify(input);
        Assert.AreEqual("helloworld123", result);
    }

    [TestMethod]
    public void Slugify_WithSpacesAndSymbols_ReturnsSlug()
    {
        var input = "Hello, World! This is a test.";
        var result = StringUtilities.Slugify(input);
        Assert.AreEqual("hello-world-this-is-a-test.", result);
    }

    [TestMethod]
    public void Slugify_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.AreEqual("", StringUtilities.Slugify(null));
        Assert.AreEqual("", StringUtilities.Slugify(""));
    }
}
