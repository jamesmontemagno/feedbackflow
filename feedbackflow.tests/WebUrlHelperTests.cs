using FeedbackFunctions.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FeedbackFlow.Tests;

[TestClass]
public class WebUrlHelperTests
{
    [TestMethod]
    public void GetWebUrl_WithConfiguredValue_ReturnsConfiguredValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://custom.domain.com"
            })
            .Build();

        // Act
        var result = WebUrlHelper.GetWebUrl(configuration);

        // Assert
        Assert.AreEqual("https://custom.domain.com", result);
    }

    [TestMethod]
    public void GetWebUrl_WithoutConfiguredValue_ReturnsDefaultValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = WebUrlHelper.GetWebUrl(configuration);

        // Assert
        Assert.AreEqual("https://www.feedbackflow.app", result);
    }

    [TestMethod]
    public void BuildUrl_WithPath_ReturnsCompleteUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();

        // Act
        var result = WebUrlHelper.BuildUrl(configuration, "/reports");

        // Assert
        Assert.AreEqual("https://test.com/reports", result);
    }

    [TestMethod]
    public void BuildUrl_WithPathMissingSlash_ReturnsCompleteUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();

        // Act
        var result = WebUrlHelper.BuildUrl(configuration, "reports");

        // Assert
        Assert.AreEqual("https://test.com/reports", result);
    }

    [TestMethod]
    public void BuildUrl_WithBaseUrlTrailingSlash_ReturnsCompleteUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com/"
            })
            .Build();

        // Act
        var result = WebUrlHelper.BuildUrl(configuration, "/reports");

        // Assert
        Assert.AreEqual("https://test.com/reports", result);
    }

    [TestMethod]
    public void BuildReportUrl_WithReportId_ReturnsCorrectUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();
        var reportId = Guid.NewGuid();

        // Act
        var result = WebUrlHelper.BuildReportUrl(configuration, reportId);

        // Assert
        Assert.AreEqual($"https://test.com/report/{reportId}", result);
    }

    [TestMethod]
    public void BuildReportUrl_WithReportIdAndSource_ReturnsCorrectUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();
        var reportId = Guid.NewGuid();

        // Act
        var result = WebUrlHelper.BuildReportUrl(configuration, reportId, "email");

        // Assert
        Assert.AreEqual($"https://test.com/report/{reportId}?source=email", result);
    }

    [TestMethod]
    public void BuildReportQueryUrl_WithReportId_ReturnsCorrectUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();
        var reportId = Guid.NewGuid();

        // Act
        var result = WebUrlHelper.BuildReportQueryUrl(configuration, reportId);

        // Assert
    Assert.AreEqual($"https://test.com/report/{reportId}", result);
    }

    [TestMethod]
    public void BuildReportQueryUrl_WithReportIdAndSource_ReturnsCorrectUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebUrl"] = "https://test.com"
            })
            .Build();
        var reportId = Guid.NewGuid();

        // Act
        var result = WebUrlHelper.BuildReportQueryUrl(configuration, reportId, "email");

        // Assert
    Assert.AreEqual($"https://test.com/report/{reportId}?source=email", result);
    }

    [TestMethod]
    public void BuildReportQueryUrl_WithDefaultConfiguration_UsesDefaultDomain()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var reportId = Guid.NewGuid();

        // Act
        var result = WebUrlHelper.BuildReportQueryUrl(configuration, reportId, "email");

        // Assert
    Assert.AreEqual($"https://www.feedbackflow.app/report/{reportId}?source=email", result);
    }
}
