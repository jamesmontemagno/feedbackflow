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
        Assert.IsTrue(adminLimits.AnalysisLimit > freeLimits.AnalysisLimit);
        Assert.IsTrue(adminLimits.AnalysisLimit > proLimits.AnalysisLimit);
        Assert.IsTrue(adminLimits.AnalysisLimit > proPlusLimits.AnalysisLimit);
        
        Assert.IsTrue(adminLimits.ReportLimit > freeLimits.ReportLimit);
        Assert.IsTrue(adminLimits.ReportLimit > proLimits.ReportLimit);
        Assert.IsTrue(adminLimits.ReportLimit > proPlusLimits.ReportLimit);
        
        Assert.IsTrue(adminLimits.FeedQueryLimit > freeLimits.FeedQueryLimit);
        Assert.IsTrue(adminLimits.FeedQueryLimit > proLimits.FeedQueryLimit);
        Assert.IsTrue(adminLimits.FeedQueryLimit > proPlusLimits.FeedQueryLimit);
        
        Assert.IsTrue(adminLimits.AnalysisRetentionDays > freeLimits.AnalysisRetentionDays);
        Assert.IsTrue(adminLimits.AnalysisRetentionDays > proLimits.AnalysisRetentionDays);
        Assert.IsTrue(adminLimits.AnalysisRetentionDays > proPlusLimits.AnalysisRetentionDays);
    }
}