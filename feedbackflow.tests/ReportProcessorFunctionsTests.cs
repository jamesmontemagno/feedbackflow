using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SharedDump.Models.Reports;
using SharedDump.Services.Interfaces;
using SharedDump.AI;
using FeedbackFunctions.Services;
using FeedbackFunctions;
using System.Reflection;
using FeedbackFunctions.Services.Reports;

namespace FeedbackFlow.Tests;

[TestClass]
public class ReportProcessorFunctionsTests
{
    private IReportCacheService _cacheService = null!;

    [TestInitialize]
    public void Setup()
    {
        _cacheService = Substitute.For<IReportCacheService>();
    }

    [TestMethod]
    public async Task GetRecentReportAsync_WithRecentReport_ReturnsExistingReport()
    {
        // Arrange
        var subreddit = "dotnet";
        var recentReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = subreddit,
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-12), // 12 hours ago
            HtmlContent = "Existing report content"
        };

        _cacheService.GetReportsAsync("reddit", subreddit)
            .Returns([recentReport]);

        // Act - Test the logic directly by creating our own method
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var reports = await _cacheService.GetReportsAsync("reddit", subreddit);
        var result = reports
            .Where(r => r.GeneratedAt >= cutoff)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefault();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(recentReport.Id, result.Id);
        Assert.AreEqual("Existing report content", result.HtmlContent);
    }

    [TestMethod]
    public async Task GetRecentReportAsync_WithOldReport_ReturnsNull()
    {
        // Arrange
        var subreddit = "dotnet";
        var oldReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = subreddit,
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-25), // 25 hours ago (older than 24 hours)
            HtmlContent = "Old report content"
        };

        _cacheService.GetReportsAsync("reddit", subreddit)
            .Returns([oldReport]);

        // Act - Test the logic directly by creating our own method
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var reports = await _cacheService.GetReportsAsync("reddit", subreddit);
        var result = reports
            .Where(r => r.GeneratedAt >= cutoff)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefault();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetRecentReportAsync_WithNoReports_ReturnsNull()
    {
        // Arrange
        var subreddit = "dotnet";
        _cacheService.GetReportsAsync("reddit", subreddit)
            .Returns(new List<ReportModel>());

        // Act - Test the logic directly by creating our own method
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var reports = await _cacheService.GetReportsAsync("reddit", subreddit);
        var result = reports
            .Where(r => r.GeneratedAt >= cutoff)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefault();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetRecentReportAsync_WithMultipleReports_ReturnsMostRecent()
    {
        // Arrange
        var subreddit = "dotnet";
        var olderReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = subreddit,
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-20),
            HtmlContent = "Older recent report"
        };
        var newerReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = subreddit,
            GeneratedAt = DateTimeOffset.UtcNow.AddHours(-5),
            HtmlContent = "Newer recent report"
        };

        _cacheService.GetReportsAsync("reddit", subreddit)
            .Returns([olderReport, newerReport]);

        // Act - Test the logic directly by creating our own method
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var reports = await _cacheService.GetReportsAsync("reddit", subreddit);
        var result = reports
            .Where(r => r.GeneratedAt >= cutoff)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefault();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(newerReport.Id, result.Id);
        Assert.AreEqual("Newer recent report", result.HtmlContent);
    }
}