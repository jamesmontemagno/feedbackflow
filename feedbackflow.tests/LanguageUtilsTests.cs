using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Utils;

namespace FeedbackFlow.Tests;

[TestClass]
public class LanguageUtilsTests
{
    [TestMethod]
    public void GetLanguageName_WithEnUS_ReturnsEnglishUnitedStates()
    {
        // Arrange
        var languageCode = "en-US";
        var expected = "English (United States)";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetLanguageName_WithESES_ReturnsSpanishSpain()
    {
        // Arrange
        var languageCode = "es-ES";
        var expected = "Spanish (Spain)";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetLanguageName_WithFrFR_ReturnsFrenchFrance()
    {
        // Arrange
        var languageCode = "fr-FR";
        var expected = "French (France)";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetLanguageName_WithNonExistentCode_ReturnsOriginalCode()
    {
        // Arrange
        var languageCode = "xx-XX"; // Non-existent code
        var expected = "xx-XX";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetLanguageName_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? languageCode = null;
        var expected = "";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetLanguageName_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var languageCode = "";
        var expected = "";

        // Act
        var result = LanguageUtils.GetLanguageName(languageCode);

        // Assert
        Assert.AreEqual(expected, result);
    }
}
