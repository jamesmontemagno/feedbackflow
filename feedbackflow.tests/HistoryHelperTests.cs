using Microsoft.VisualStudio.TestTools.UnitTesting;
using FeedbackWebApp.Services;
using System.Reflection;

namespace FeedbackFlow.Tests;

[TestClass]
public class HistoryHelperTests
{
    private HistoryHelper _historyHelper;

    [TestInitialize]
    public void Setup()
    {
        _historyHelper = new HistoryHelper();
    }

    [TestMethod]
    public void ConvertMarkdownToHtml_ShouldNotExceedSixHeaderLevels()
    {
        // Arrange
        var markdown = @"##### Fifth Level Header
###### Sixth Level Header";

        // Act
        var result = _historyHelper.ConvertMarkdownToHtml(markdown);

        // Assert
        Assert.IsFalse(result.Contains("<h7>"), "Should not create H7 tags");
        Assert.IsFalse(result.Contains("<h8>"), "Should not create H8 tags");
    }

    [TestMethod]
    public void ConvertMarkdownToHtml_ShouldHandleEmptyOrNullMarkdown()
    {
        // Arrange & Act
        var result1 = _historyHelper.ConvertMarkdownToHtml(null);
        var result2 = _historyHelper.ConvertMarkdownToHtml("");
        var result3 = _historyHelper.ConvertMarkdownToHtml("   ");

        // Assert
        Assert.AreEqual("", result1);
        Assert.AreEqual("", result2);
        Assert.IsNotNull(result3);
    }
}
