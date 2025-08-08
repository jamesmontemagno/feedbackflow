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

    [TestMethod]
    public void GetFirst5Digits_NormalUserId_ReturnsFirst5Characters()
    {
        var userId = "abcdefghijk123456";
        var result = StringUtilities.GetFirst5Digits(userId);
        Assert.AreEqual("abcde", result);
    }

    [TestMethod]
    public void GetFirst5Digits_ShortUserId_ReturnsFullUserId()
    {
        var userId = "abc";
        var result = StringUtilities.GetFirst5Digits(userId);
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void GetFirst5Digits_ExactlyFiveCharacters_ReturnsAllCharacters()
    {
        var userId = "abcde";
        var result = StringUtilities.GetFirst5Digits(userId);
        Assert.AreEqual("abcde", result);
    }

    [TestMethod]
    public void GetFirst5Digits_NullOrEmpty_ReturnsNA()
    {
        Assert.AreEqual("N/A", StringUtilities.GetFirst5Digits(null));
        Assert.AreEqual("N/A", StringUtilities.GetFirst5Digits(""));
        Assert.AreEqual("N/A", StringUtilities.GetFirst5Digits("   "));
    }
}
