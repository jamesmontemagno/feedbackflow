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
        Assert.IsGreaterThan(0, userStats.TotalUsers);
        Assert.IsGreaterThanOrEqualTo(0, userStats.NewUsersLast7Days);
        Assert.IsGreaterThanOrEqualTo(userStats.NewUsersLast7Days, userStats.NewUsersLast30Days);
        Assert.IsGreaterThan(0, userStats.TierDistribution.Count);
        Assert.IsGreaterThanOrEqualTo(0, userStats.EmailNotificationsEnabledCount);
        Assert.IsGreaterThanOrEqualTo(0, userStats.ApiEnabledUsers);
        Assert.IsGreaterThanOrEqualTo(0, userStats.ActiveUsersLast14Days);
        Assert.AreEqual(userStats.TotalUsers, userStats.ActiveUsersLast14Days + userStats.InactiveUsers);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidUsageStatistics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var usageStats = result.UsageStats;
        Assert.IsGreaterThanOrEqualTo(0, usageStats.TotalAnalysesUsed);
        Assert.IsGreaterThanOrEqualTo(0, usageStats.TotalFeedQueriesUsed);
        Assert.IsGreaterThanOrEqualTo(0, usageStats.TotalActiveReports);
        Assert.IsGreaterThanOrEqualTo(0, usageStats.TotalApiCalls);
        Assert.IsNotNull(usageStats.TierUsageMetrics);
        Assert.IsNotNull(usageStats.TopUsers);
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveValidApiStatistics()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        var apiStats = result.ApiStats;
        Assert.IsGreaterThanOrEqualTo(0, apiStats.TotalApiEnabledUsers);
        Assert.IsGreaterThanOrEqualTo(0, apiStats.TotalApiCalls);
        Assert.IsGreaterThanOrEqualTo(0, apiStats.AverageApiCallsPerUser);
        Assert.IsGreaterThanOrEqualTo(0, apiStats.RecentlyActiveApiUsers);
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
            Assert.IsFalse(string.IsNullOrEmpty(user.MaskedUserId));
            Assert.Contains("*", user.MaskedUserId);
            Assert.AreNotEqual(user.UserId, user.MaskedUserId, "User ID should be masked");
        }

        foreach (var apiUser in result.ApiStats.TopApiUsers)
        {
            Assert.IsFalse(string.IsNullOrEmpty(apiUser.MaskedUserId));
            Assert.Contains("*", apiUser.MaskedUserId);
            Assert.AreNotEqual(apiUser.UserId, apiUser.MaskedUserId, "API user ID should be masked");
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
        Assert.IsLessThanOrEqualTo(0.1, Math.Abs(totalPercentage - 100.0), "Tier percentages should add up to approximately 100%");
    }

    [TestMethod]
    public async Task GetDashboardMetricsAsync_ShouldHaveRealisticMockData()
    {
        // Act
        var result = await _mockService!.GetDashboardMetricsAsync();

        // Assert
        // Verify the mock data follows expected patterns
        Assert.IsGreaterThan(100, result.UserStats.TotalUsers, "Should have realistic user count");
        Assert.IsGreaterThan(0, result.UsageStats.TotalAnalysesUsed, "Should have analysis usage");
        Assert.IsGreaterThan(0, result.ApiStats.TotalApiCalls, "Should have API usage");
        
        // Verify tier distribution is realistic
        var tierDistribution = result.UserStats.TierDistribution;
        if (tierDistribution.ContainsKey("Free"))
        {
            Assert.IsGreaterThan(tierDistribution.GetValueOrDefault("Pro", 0), tierDistribution["Free"],
                "Free tier should have more users than Pro");
        }
    }
}