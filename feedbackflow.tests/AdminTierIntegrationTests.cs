using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using FeedbackFunctions.Services.Account;

namespace FeedbackFlow.Tests;

[TestClass]
public class AdminTierIntegrationTests
{
    [TestMethod]
    [TestCategory("Account")]
    public void AdminTier_ShouldHaveSameLimitsAsSuperUser()
    {
        // Arrange
        var userAccountService = new UserAccountService("UseDevelopmentStorage=true");
        
        // Act
        var superUserLimits = userAccountService.GetLimitsForTier(AccountTier.SuperUser);
        var adminLimits = userAccountService.GetLimitsForTier(AccountTier.Admin);
        
        // Assert
        Assert.AreEqual(superUserLimits.AnalysisLimit, adminLimits.AnalysisLimit);
        Assert.AreEqual(superUserLimits.ReportLimit, adminLimits.ReportLimit);
        Assert.AreEqual(superUserLimits.FeedQueryLimit, adminLimits.FeedQueryLimit);
        Assert.AreEqual(superUserLimits.AnalysisRetentionDays, adminLimits.AnalysisRetentionDays);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void AdminTier_ShouldHaveHighLimits()
    {
        // Arrange
        var userAccountService = new UserAccountService("UseDevelopmentStorage=true");
        
        // Act
        var adminLimits = userAccountService.GetLimitsForTier(AccountTier.Admin);
        
        // Assert
        Assert.AreEqual(10000, adminLimits.AnalysisLimit);
        Assert.AreEqual(10000, adminLimits.ReportLimit);
        Assert.AreEqual(10000, adminLimits.FeedQueryLimit);
        Assert.AreEqual(3650, adminLimits.AnalysisRetentionDays); // 10 years
    }
    
    [TestMethod]
    [TestCategory("Account")]
    public void AdminTier_ShouldHaveHigherLimitsThanRegularTiers()
    {
        // Arrange
        var userAccountService = new UserAccountService("UseDevelopmentStorage=true");
        
        // Act
        var freeLimits = userAccountService.GetLimitsForTier(AccountTier.Free);
        var proLimits = userAccountService.GetLimitsForTier(AccountTier.Pro);
        var proPlusLimits = userAccountService.GetLimitsForTier(AccountTier.ProPlus);
        var adminLimits = userAccountService.GetLimitsForTier(AccountTier.Admin);
        
        // Assert - Admin should have higher limits than all regular tiers
        Assert.IsGreaterThan(freeLimits.AnalysisLimit, adminLimits.AnalysisLimit);
        Assert.IsGreaterThan(proLimits.AnalysisLimit, adminLimits.AnalysisLimit);
        Assert.IsGreaterThan(proPlusLimits.AnalysisLimit, adminLimits.AnalysisLimit);
        
        Assert.IsGreaterThan(freeLimits.ReportLimit, adminLimits.ReportLimit);
        Assert.IsGreaterThan(proLimits.ReportLimit, adminLimits.ReportLimit);
        Assert.IsGreaterThan(proPlusLimits.ReportLimit, adminLimits.ReportLimit);
        
        Assert.IsGreaterThan(freeLimits.FeedQueryLimit, adminLimits.FeedQueryLimit);
        Assert.IsGreaterThan(proLimits.FeedQueryLimit, adminLimits.FeedQueryLimit);
        Assert.IsGreaterThan(proPlusLimits.FeedQueryLimit, adminLimits.FeedQueryLimit);
        
        Assert.IsGreaterThan(freeLimits.AnalysisRetentionDays, adminLimits.AnalysisRetentionDays);
        Assert.IsGreaterThan(proLimits.AnalysisRetentionDays, adminLimits.AnalysisRetentionDays);
        Assert.IsGreaterThan(proPlusLimits.AnalysisRetentionDays, adminLimits.AnalysisRetentionDays);
    }
}