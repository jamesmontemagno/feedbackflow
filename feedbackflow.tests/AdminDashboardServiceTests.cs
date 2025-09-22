using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Admin;
using SharedDump.Models.Account;
using FeedbackWebApp.Services.Mock;

namespace FeedbackFlow.Tests;

[TestClass]
public class AdminDashboardServiceTests
{
    private MockAdminDashboardService? _mockService;
    private ILogger<MockAdminDashboardService>? _logger;

    [TestInitialize]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<MockAdminDashboardService>();
        _mockService = new MockAdminDashboardService(_logger);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldReturnValidMetrics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.UserStats);
        Assert.IsNotNull(result.UsageStats);
        Assert.IsNotNull(result.ApiStats);
        Assert.IsTrue(result.GeneratedAt <= DateTime.UtcNow);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidUserStatistics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var userStats = result.UserStats;
        Assert.IsTrue(userStats.TotalUsers > 0);
        Assert.IsTrue(userStats.NewUsersLast7Days >= 0);
        Assert.IsTrue(userStats.NewUsersLast30Days >= userStats.NewUsersLast7Days);
        Assert.IsTrue(userStats.TierDistribution.Count > 0);
        Assert.IsTrue(userStats.EmailNotificationsEnabledCount >= 0);
        Assert.IsTrue(userStats.ApiEnabledUsers >= 0);
        Assert.IsTrue(userStats.ActiveUsersLast14Days >= 0);
        Assert.AreEqual(userStats.TotalUsers, userStats.ActiveUsersLast14Days + userStats.InactiveUsers);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidUsageStatistics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var usageStats = result.UsageStats;
        Assert.IsTrue(usageStats.TotalAnalysesUsed >= 0);
        Assert.IsTrue(usageStats.TotalFeedQueriesUsed >= 0);
        Assert.IsTrue(usageStats.TotalActiveReports >= 0);
        Assert.IsTrue(usageStats.TotalApiCalls >= 0);
        Assert.IsNotNull(usageStats.TierUsageMetrics);
        Assert.IsNotNull(usageStats.TopUsers);
        Assert.IsNotNull(usageStats.TopReports);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidApiStatistics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var apiStats = result.ApiStats;
        Assert.IsTrue(apiStats.TotalApiEnabledUsers >= 0);
        Assert.IsTrue(apiStats.TotalApiCalls >= 0);
        Assert.IsTrue(apiStats.AverageApiCallsPerUser >= 0);
        Assert.IsTrue(apiStats.RecentlyActiveApiUsers >= 0);
        Assert.IsNotNull(apiStats.TopApiUsers);
        Assert.IsNotNull(apiStats.EndpointDistribution);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldMaskUserIds()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        foreach (var user in result.UsageStats.TopUsers)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(user.MaskedUserId));
            Assert.IsTrue(user.MaskedUserId.Contains("*"));
            Assert.IsFalse(user.UserId == user.MaskedUserId, "User ID should be masked");
        }

        foreach (var apiUser in result.ApiStats.TopApiUsers)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(apiUser.MaskedUserId));
            Assert.IsTrue(apiUser.MaskedUserId.Contains("*"));
            Assert.IsFalse(apiUser.UserId == apiUser.MaskedUserId, "API user ID should be masked");
        }
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveConsistentTierData()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var userStats = result.UserStats;
        var totalFromTiers = userStats.TierDistribution.Values.Sum();
        
        Assert.AreEqual(userStats.TotalUsers, totalFromTiers, 
            "Total users should equal sum of tier distribution");

        // Check that percentages add up to approximately 100%
        var totalPercentage = userStats.TierDistributionPercentage.Values.Sum();
        Assert.IsTrue(Math.Abs(totalPercentage - 100.0) < 0.1, 
            "Tier percentages should add up to approximately 100%");
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveRealisticMockData()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        // Verify the mock data follows expected patterns
        Assert.IsTrue(result.UserStats.TotalUsers > 100, "Should have realistic user count");
        Assert.IsTrue(result.UsageStats.TotalAnalysesUsed > 0, "Should have analysis usage");
        Assert.IsTrue(result.ApiStats.TotalApiCalls > 0, "Should have API usage");
        
        // Verify tier distribution is realistic
        var tierDistribution = result.UserStats.TierDistribution;
        if (tierDistribution.ContainsKey("Free"))
        {
            Assert.IsTrue(tierDistribution["Free"] > tierDistribution.GetValueOrDefault("Pro", 0),
                "Free tier should have more users than Pro");
        }
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidTopReports()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var usageStats = result.UsageStats;
        Assert.IsNotNull(usageStats.TopReports);
        
        // Each report should have valid properties
        foreach (var report in usageStats.TopReports)
        {
            Assert.IsFalse(string.IsNullOrEmpty(report.Type));
            Assert.IsFalse(string.IsNullOrEmpty(report.DisplayName));
            Assert.IsFalse(string.IsNullOrEmpty(report.Source));
            Assert.IsTrue(report.SubscriberCount > 0, "Subscriber count should be positive");
            Assert.IsTrue(report.CreatedAt <= DateTime.UtcNow, "CreatedAt should not be in the future");
        }
    }
}