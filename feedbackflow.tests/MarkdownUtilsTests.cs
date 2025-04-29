using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class MarkdownUtilsTests
{
    [TestMethod]
    public void CleanForSpeech_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? markdown = null;
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var markdown = string.Empty;
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithHeaders_RemovesHeaderSymbols()
    {
        // Arrange
        var markdown = "# Header 1\n## Header 2\n### Header 3";
        var expected = "Header 1. Header 2. Header 3";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithEmphasis_RemovesEmphasisMarkers()
    {
        // Arrange
        var markdown = "This is *italic*, **bold**, and ***bold italic*** text.";
        var expected = "This is italic, bold, and bold italic text.";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithUnderscoreEmphasis_RemovesUnderscores()
    {
        // Arrange
        var markdown = "This is _italic_, __bold__, and ___bold italic___ text.";
        var expected = "This is italic, bold, and bold italic text.";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithCodeBlocks_RemovesCodeBlocks()
    {
        // Arrange
        var markdown = "Here is some code:\n```csharp\nvar x = 5;\nconsole.log(x);\n```\nAnd more text.";
        var expected = "Here is some code: And more text.";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithInlineCode_RemovesInlineCode()
    {
        // Arrange
        var markdown = "Use the `console.log()` function.";
        var expected = "Use the console.log() function.";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithLinks_KeepsLinkText()
    {
        // Arrange
        var markdown = "Visit [FeedbackFlow](https://feedbackflow.com) for more information.";
        var expected = "Visit FeedbackFlow for more information.";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithImages_RemovesImages()
    {
        // Arrange
        var markdown = "Here's an image: ![FeedbackFlow logo](https://feedbackflow.com/logo.png)";
        var expected = "Here's an image:";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithLists_CleansBulletPoints()
    {
        // Arrange
        var markdown = "My list:\n- Item 1\n- Item 2\n- Item 3";
        var expected = "My list: • Item 1. • Item 2. • Item 3";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithNumberedLists_CleansBulletPoints()
    {
        // Arrange
        var markdown = "My list:\n1. First item\n2. Second item\n3. Third item";
        var expected = "My list: • First item. • Second item. • Third item";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithBlockquotes_RemovesBlockquoteMarkers()
    {
        // Arrange
        var markdown = "As someone said:\n> This is a quote\n> And it continues";
        var expected = "As someone said: This is a quote. And it continues";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithHorizontalRules_RemovesHorizontalRules()
    {
        // Arrange
        var markdown = "Above the line\n---\nBelow the line";
        var expected = "Above the line. Below the line";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
    
    [TestMethod]
    public void CleanForSpeech_WithComplexMarkdown_ReturnsCleanText()
    {
        // Arrange
        var markdown = "# Welcome to FeedbackFlow\n\n" +
                      "This is a **powerful** tool for *analyzing feedback*.\n\n" +
                      "## Features\n\n" +
                      "- Real-time analysis\n" +
                      "- Multi-platform support\n" +
                      "- AI-powered insights\n\n" +
                      "Visit [our website](https://feedbackflow.com) for more info.\n\n" +
                      "![Dashboard](https://example.com/dashboard.png)\n\n" +
                      "```csharp\nvar app = new FeedbackFlowApp();\napp.Run();\n```\n\n" +
                      "> Customer feedback is the lifeblood of any business.\n\n" +
                      "***\n\n" +
                      "Thank you for using FeedbackFlow!";
                      
        var expected = "Welcome to FeedbackFlow. This is a powerful tool for analyzing feedback. " +
                      "Features. • Real-time analysis. • Multi-platform support. • AI-powered insights. " +
                      "Visit our website for more info. " +
                      "Customer feedback is the lifeblood of any business. " +
                      "Thank you for using FeedbackFlow!";
        
        // Act
        var result = MarkdownUtils.CleanForSpeech(markdown);
        
        // Assert
        Assert.AreEqual(expected, result);
    }
}
