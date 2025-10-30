using SharedDump.AI;

namespace FeedbackFlow.Tests;

[TestClass]
public class PromptTypeTests
{
    [TestMethod]
    public void GetPromptByType_ProductFeedback_ReturnsValidPrompt()
    {
        // Arrange
        var promptType = PromptType.ProductFeedback;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert
        Assert.IsNotNull(prompt);
        Assert.IsTrue(prompt.Length > 0);
        Assert.IsTrue(prompt.Contains("Product Feedback"));
    }

    [TestMethod]
    public void GetPromptByType_CompetitorAnalysis_ReturnsValidPrompt()
    {
        // Arrange
        var promptType = PromptType.CompetitorAnalysis;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert
        Assert.IsNotNull(prompt);
        Assert.IsTrue(prompt.Length > 0);
        Assert.IsTrue(prompt.Contains("Competitor"));
    }

    [TestMethod]
    public void GetPromptByType_GeneralAnalysis_ReturnsValidPrompt()
    {
        // Arrange
        var promptType = PromptType.GeneralAnalysis;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert
        Assert.IsNotNull(prompt);
        Assert.IsTrue(prompt.Length > 0);
        Assert.IsTrue(prompt.Contains("General"));
    }

    [TestMethod]
    public void GetPromptByType_AllTypes_ReturnDistinctPrompts()
    {
        // Arrange & Act
        var productPrompt = FeedbackAnalyzerService.GetPromptByType(PromptType.ProductFeedback);
        var competitorPrompt = FeedbackAnalyzerService.GetPromptByType(PromptType.CompetitorAnalysis);
        var generalPrompt = FeedbackAnalyzerService.GetPromptByType(PromptType.GeneralAnalysis);

        // Assert
        Assert.AreNotEqual(productPrompt, competitorPrompt);
        Assert.AreNotEqual(productPrompt, generalPrompt);
        Assert.AreNotEqual(competitorPrompt, generalPrompt);
    }

    [TestMethod]
    public void GetUniversalPrompt_ReturnsProductFeedbackPrompt()
    {
        // Act
        var universalPrompt = FeedbackAnalyzerService.GetUniversalPrompt();
        var productPrompt = FeedbackAnalyzerService.GetPromptByType(PromptType.ProductFeedback);

        // Assert
        Assert.AreEqual(productPrompt, universalPrompt);
    }

    [TestMethod]
    public void GetPromptByType_ProductFeedback_ContainsKeyPhrases()
    {
        // Arrange
        var promptType = PromptType.ProductFeedback;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert - check for key phrases that should be in product feedback prompt
        Assert.IsTrue(prompt.Contains("YOUR product"));
        Assert.IsTrue(prompt.Contains("Feature Requests") || prompt.Contains("feature"));
        Assert.IsTrue(prompt.Contains("Pain Points") || prompt.Contains("pain"));
    }

    [TestMethod]
    public void GetPromptByType_CompetitorAnalysis_ContainsKeyPhrases()
    {
        // Arrange
        var promptType = PromptType.CompetitorAnalysis;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert - check for key phrases that should be in competitor analysis prompt
        Assert.IsTrue(prompt.Contains("competitor"));
        Assert.IsTrue(prompt.Contains("Strengths") || prompt.Contains("strengths"));
        Assert.IsTrue(prompt.Contains("Weaknesses") || prompt.Contains("weaknesses"));
        Assert.IsTrue(prompt.Contains("competitive"));
    }

    [TestMethod]
    public void GetPromptByType_GeneralAnalysis_ContainsKeyPhrases()
    {
        // Arrange
        var promptType = PromptType.GeneralAnalysis;

        // Act
        var prompt = FeedbackAnalyzerService.GetPromptByType(promptType);

        // Assert - check for key phrases that should be in general analysis prompt
        Assert.IsTrue(prompt.Contains("objective") || prompt.Contains("neutral"));
        Assert.IsTrue(prompt.Contains("General") || prompt.Contains("Content"));
        Assert.IsTrue(prompt.Contains("balanced"));
    }
}
