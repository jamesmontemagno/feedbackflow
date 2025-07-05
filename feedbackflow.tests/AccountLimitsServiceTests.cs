using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using FeedbackFunctions.Services.Account;

namespace FeedbackFlow.Tests;

[TestClass]
public class AccountLimitsServiceTests
{
    private IUserAccountTableService _mockUserTable = null!;
    private IUsageRecordTableService _mockUsageTable = null!;
    private AccountLimitsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockUserTable = new MockUserAccountTableService();
        _mockUsageTable = new MockUsageRecordTableService();
        _service = new AccountLimitsService(_mockUserTable, _mockUsageTable);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetLimitsForTier_Free_ShouldReturnCorrectLimits()
    {
        var limits = _service.GetLimitsForTier(AccountTier.Free);
        
        Assert.AreEqual(10, limits.AnalysisLimit);
        Assert.AreEqual(1, limits.ReportLimit);
        Assert.AreEqual(20, limits.FeedQueryLimit);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetLimitsForTier_Pro_ShouldReturnCorrectLimits()
    {
        var limits = _service.GetLimitsForTier(AccountTier.Pro);
        
        Assert.AreEqual(75, limits.AnalysisLimit);
        Assert.AreEqual(5, limits.ReportLimit);
        Assert.AreEqual(200, limits.FeedQueryLimit);
    }

    [TestMethod]
    [TestCategory("Account")]
    public void GetLimitsForTier_ProPlus_ShouldReturnCorrectLimits()
    {
        var limits = _service.GetLimitsForTier(AccountTier.ProPlus);
        
        Assert.AreEqual(300, limits.AnalysisLimit);
        Assert.AreEqual(25, limits.ReportLimit);
        Assert.AreEqual(1000, limits.FeedQueryLimit);
    }

    // Mock implementations for testing
    private class MockUserAccountTableService : IUserAccountTableService
    {
        public Task<UserAccountEntity?> GetUserAccountAsync(string userId) => Task.FromResult<UserAccountEntity?>(null);
        public Task UpsertUserAccountAsync(UserAccountEntity entity) => Task.CompletedTask;
        public Task CreateTableIfNotExistsAsync() => Task.CompletedTask;
        public Task<int> ResetAllMonthlyUsageAsync() => Task.FromResult(0);
    }

    private class MockUsageRecordTableService : IUsageRecordTableService
    {
        public Task AddUsageRecordAsync(UsageRecordEntity entity) => Task.CompletedTask;
        public Task<List<UsageRecordEntity>> GetUsageRecordsForUserAsync(string userId) => Task.FromResult(new List<UsageRecordEntity>());
        public Task CreateTableIfNotExistsAsync() => Task.CompletedTask;
    }
}
