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
    public void ConvertMarkdownToHtml_ShouldIncreaseHeaderLevelsByTwo()
    {
        // Arrange
        var markdown = @"# Main Header
This is some content.

## Second Level Header
More content here.

### Third Level Header
Even more content.";

        // Act
        var result = _historyHelper.ConvertMarkdownToHtml(markdown);

        // Assert
        Assert.IsTrue(result.Contains("<h3>Main Header</h3>"), "H1 should become H3");
        Assert.IsTrue(result.Contains("<h4>Second Level Header</h4>"), "H2 should become H4");
        Assert.IsTrue(result.Contains("<h5>Third Level Header</h5>"), "H3 should become H5");
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

    [TestMethod]
    public void ConvertMarkdownToHtml_ShouldPreserveNonHeaderContent()
    {
        // Arrange
        var markdown = @"# Header
This is regular text.
- List item 1
- List item 2

**Bold text** and *italic text*.";

        // Act
        var result = _historyHelper.ConvertMarkdownToHtml(markdown);

        // Assert
        Assert.IsTrue(result.Contains("<h3>Header</h3>"), "Header should be adjusted");
        Assert.IsTrue(result.Contains("<li>List item 1</li>"), "List items should be preserved");
        Assert.IsTrue(result.Contains("<strong>Bold text</strong>"), "Bold formatting should be preserved");
        Assert.IsTrue(result.Contains("<em>italic text</em>"), "Italic formatting should be preserved");
    }
}
