using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FeedbackFunctions.Services;
using SharedDump.Models.Reports;
using System.Text;
using System.Text.Json;
using Azure;

namespace FeedbackFlow.Tests;

[TestClass]
public class ReportCacheServiceTests
{
    private ILogger<ReportCacheService> _logger = null!;
    private BlobContainerClient _mockContainerClient = null!;
    private ReportCacheService _cacheService = null!;
    private ReportModel _testReport = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<ReportCacheService>>();
        _mockContainerClient = Substitute.For<BlobContainerClient>();
        _cacheService = new ReportCacheService(_logger, _mockContainerClient);
        
        _testReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = "dotnet",
            GeneratedAt = DateTime.UtcNow,
            ThreadCount = 10,
            CommentCount = 100,
            CutoffDate = DateTime.UtcNow.AddDays(-7),
            HtmlContent = "Test report content"
        };
    }

    [TestMethod]
    public async Task GetReportAsync_ReportInCache_ReturnsCachedReport()
    {
        // Arrange
        var reportId = _testReport.Id.ToString();
        await _cacheService.SetReportAsync(_testReport);

        // Act
        var result = await _cacheService.GetReportAsync(reportId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(_testReport.Id, result.Id);
        Assert.AreEqual(_testReport.Source, result.Source);
        Assert.AreEqual(_testReport.SubSource, result.SubSource);
    }

    [TestMethod]
    public async Task GetReportAsync_ReportNotInCache_ReturnsNull()
    {
        // Arrange
        var reportId = Guid.NewGuid().ToString();
        
        // Mock blob client to return that blob doesn't exist
        var mockBlobClient = Substitute.For<BlobClient>();
        mockBlobClient.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
        _mockContainerClient.GetBlobClient($"{reportId}.json").Returns(mockBlobClient);

        // Act
        var result = await _cacheService.GetReportAsync(reportId);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SetReportAsync_NewReport_AddsToCache()
    {
        // Arrange
        var reportId = _testReport.Id.ToString();

        // Act
        await _cacheService.SetReportAsync(_testReport);
        var result = await _cacheService.GetReportAsync(reportId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(_testReport.Id, result.Id);
    }

    [TestMethod]
    public async Task GetReportsAsync_WithSourceFilter_ReturnsFilteredReports()
    {
        // Arrange
        var redditReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = "dotnet",
            GeneratedAt = DateTime.UtcNow,
            ThreadCount = 5,
            CommentCount = 50,
            CutoffDate = DateTime.UtcNow.AddDays(-7),
            HtmlContent = "Reddit report"
        };

        var githubReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "github",
            SubSource = "microsoft/dotnet",
            GeneratedAt = DateTime.UtcNow,
            ThreadCount = 3,
            CommentCount = 30,
            CutoffDate = DateTime.UtcNow.AddDays(-7),
            HtmlContent = "GitHub report"
        };

        // Initialize cache with reports
        await _cacheService.SetReportAsync(redditReport);
        await _cacheService.SetReportAsync(githubReport);

        // Act
        var redditResults = await _cacheService.GetReportsAsync("reddit");
        var githubResults = await _cacheService.GetReportsAsync("github");
        var allResults = await _cacheService.GetReportsAsync();

        // Assert
        Assert.AreEqual(1, redditResults.Count);
        Assert.AreEqual("reddit", redditResults[0].Source);
        
        Assert.AreEqual(1, githubResults.Count);
        Assert.AreEqual("github", githubResults[0].Source);
        
        Assert.AreEqual(2, allResults.Count);
    }

    [TestMethod]
    public async Task GetReportsAsync_WithSourceAndSubsourceFilter_ReturnsFilteredReports()
    {
        // Arrange
        var dotnetReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = "dotnet",
            GeneratedAt = DateTime.UtcNow,
            ThreadCount = 5,
            CommentCount = 50,
            CutoffDate = DateTime.UtcNow.AddDays(-7),
            HtmlContent = "Dotnet report"
        };

        var csharpReport = new ReportModel
        {
            Id = Guid.NewGuid(),
            Source = "reddit",
            SubSource = "csharp",
            GeneratedAt = DateTime.UtcNow,
            ThreadCount = 3,
            CommentCount = 30,
            CutoffDate = DateTime.UtcNow.AddDays(-7),
            HtmlContent = "CSharp report"
        };

        // Initialize cache with reports
        await _cacheService.SetReportAsync(dotnetReport);
        await _cacheService.SetReportAsync(csharpReport);

        // Act
        var filteredResults = await _cacheService.GetReportsAsync("reddit", "dotnet");

        // Assert
        Assert.AreEqual(1, filteredResults.Count);
        Assert.AreEqual("dotnet", filteredResults[0].SubSource);
    }

    [TestMethod]
    public async Task RemoveReportAsync_ExistingReport_RemovesFromCache()
    {
        // Arrange
        var reportId = _testReport.Id.ToString();
        await _cacheService.SetReportAsync(_testReport);

        // Verify report is in cache
        var beforeRemoval = await _cacheService.GetReportAsync(reportId);
        Assert.IsNotNull(beforeRemoval);

        // Act
        await _cacheService.RemoveReportAsync(reportId);
        var afterRemoval = await _cacheService.GetReportAsync(reportId);

        // Assert
        Assert.IsNull(afterRemoval);
    }

    [TestMethod]
    public async Task ClearCacheAsync_WithCachedReports_ClearsAllReports()
    {
        // Arrange
        await _cacheService.SetReportAsync(_testReport);
        var beforeClear = await _cacheService.GetReportsAsync();
        Assert.IsTrue(beforeClear.Count > 0);

        // Act
        await _cacheService.ClearCacheAsync();
        var afterClear = await _cacheService.GetReportsAsync();

        // Assert
        Assert.AreEqual(0, afterClear.Count);
    }

    [TestMethod]
    public async Task GetCacheStatusAsync_NewCache_ReturnsCorrectStatus()
    {
        // Act
        var status = await _cacheService.GetCacheStatusAsync();

        // Assert
        Assert.AreEqual(0, status.ReportCount);
        Assert.AreEqual(DateTime.MinValue, status.LastRefresh);
    }

    [TestMethod]
    public async Task GetCacheStatusAsync_WithCachedReports_ReturnsCorrectCount()
    {
        // Arrange
        await _cacheService.SetReportAsync(_testReport);

        // Act
        var status = await _cacheService.GetCacheStatusAsync();

        // Assert
        Assert.AreEqual(1, status.ReportCount);
    }
}
