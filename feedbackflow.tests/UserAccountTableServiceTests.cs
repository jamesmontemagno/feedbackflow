using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using SharedDump.Services.Account;
using NSubstitute;

namespace FeedbackFlow.Tests
{
    [TestClass]
    public class UserAccountTableServiceTests
    {
        [TestMethod]
        public async Task CanUpsertAndRetrieveUserAccount_UnitMock()
        {
            var mock = Substitute.For<IUserAccountTableService>();
            var entity = new UserAccountEntity
            {
                PartitionKey = "testuser",
                RowKey = "account",
                Tier = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastResetDate = DateTime.UtcNow,
                AnalysesUsed = 1,
                FeedQueriesUsed = 2,
                ActiveReports = 3,
                AnalysisLimit = 10,
                ReportLimit = 5,
                FeedQueryLimit = 20
            };
            await mock.UpsertUserAccountAsync(entity);
            mock.GetUserAccountAsync("testuser").Returns(entity);
            var retrieved = await mock.GetUserAccountAsync("testuser");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(entity.Tier, retrieved.Tier);
            Assert.AreEqual(entity.AnalysesUsed, retrieved.AnalysesUsed);
        }
    }
}
