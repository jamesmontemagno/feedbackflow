using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using FeedbackWebApp.Services.Account;

namespace FeedbackFlow.Tests;

[TestClass]
public class WebAppAccountServiceTests
{
    [TestMethod]
    [TestCategory("Account")]
    public async Task GetUserAccountAndLimitsAsync_WithTierInfo_ShouldUseTierInfoLimits()
    {
        // Arrange
        var mockService = new MockWebAppAccountService(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockWebAppAccountService>.Instance);
        
        var tierInfo = new TierInfo[]
        {
            new()
            {
                Tier = AccountTier.Free,
                Name = "Free",
                Description = "Test tier",
                Price = "Free",
                Features = new[] { "Basic" },
                Limits = new AccountLimits
                {
                    AnalysisLimit = 15, // Different from default
                    ReportLimit = 2,    // Different from default  
                    FeedQueryLimit = 25 // Different from default
                }
            }
        };

        // Act - Call with tier info provided
        var resultWithTierInfo = await mockService.GetUserAccountAndLimitsAsync(tierInfo);

        // Assert
        Assert.IsNotNull(resultWithTierInfo);
        Assert.IsNotNull(resultWithTierInfo.Value.account);
        Assert.IsNotNull(resultWithTierInfo.Value.limits);
        
        // Should use limits from provided tier info
        Assert.AreEqual(15, resultWithTierInfo.Value.limits.AnalysisLimit);
        Assert.AreEqual(2, resultWithTierInfo.Value.limits.ReportLimit);
        Assert.AreEqual(25, resultWithTierInfo.Value.limits.FeedQueryLimit);
    }

    [TestMethod]
    [TestCategory("Account")]
    public async Task GetUserAccountAndLimitsAsync_WithoutTierInfo_ShouldUseFallbackLimits()
    {
        // Arrange
        var mockService = new MockWebAppAccountService(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockWebAppAccountService>.Instance);

        // Act - Call without tier info (fallback to defaults)
        var resultWithoutTierInfo = await mockService.GetUserAccountAndLimitsAsync();

        // Assert
        Assert.IsNotNull(resultWithoutTierInfo);
        Assert.IsNotNull(resultWithoutTierInfo.Value.account);
        Assert.IsNotNull(resultWithoutTierInfo.Value.limits);
        
        // Should use default limits for Free tier
        Assert.AreEqual(10, resultWithoutTierInfo.Value.limits.AnalysisLimit);
        Assert.AreEqual(1, resultWithoutTierInfo.Value.limits.ReportLimit);
        Assert.AreEqual(20, resultWithoutTierInfo.Value.limits.FeedQueryLimit);
    }

    [TestMethod]
    [TestCategory("Account")]
    public async Task GetUserAccountAndLimitsAsync_WithMissingTierInTierInfo_ShouldUseFallbackLimits()
    {
        // Arrange
        var mockService = new MockWebAppAccountService(Microsoft.Extensions.Logging.Abstractions.NullLogger<MockWebAppAccountService>.Instance);
        
        var tierInfo = new TierInfo[]
        {
            new()
            {
                Tier = AccountTier.Pro, // Only Pro tier, but user is Free
                Name = "Pro",
                Description = "Test tier", 
                Price = "$19/month",
                Features = new[] { "Advanced" },
                Limits = new AccountLimits
                {
                    AnalysisLimit = 75,
                    ReportLimit = 5,
                    FeedQueryLimit = 200
                }
            }
        };

        // Act - Call with tier info that doesn't contain user's tier
        var result = await mockService.GetUserAccountAndLimitsAsync(tierInfo);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Value.account);
        Assert.IsNotNull(result.Value.limits);
        
        // Should fall back to default limits since Free tier not found in provided tier info
        Assert.AreEqual(10, result.Value.limits.AnalysisLimit);
        Assert.AreEqual(1, result.Value.limits.ReportLimit);
        Assert.AreEqual(20, result.Value.limits.FeedQueryLimit);
    }
}
