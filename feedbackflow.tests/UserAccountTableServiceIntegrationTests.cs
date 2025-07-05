using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.Account;
using SharedDump.Services.Account;

namespace FeedbackFlow.Tests
{
    [TestClass]
    public class UserAccountTableServiceIntegrationTests
    {
        private const string StorageConnection = "UseDevelopmentStorage=true";
        private IUserAccountTableService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new UserAccountTableService(StorageConnection);
            _service.CreateTableIfNotExistsAsync().GetAwaiter().GetResult();
        }

        [TestMethod]
        [TestCategory("Integration")]
        [Ignore("Integration test - requires Azurite or Azure Storage.")]
        public async Task CanUpsertAndRetrieveUserAccount_Integration()
        {
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
                ActiveReports = 3
            };
            await _service.UpsertUserAccountAsync(entity);
            var retrieved = await _service.GetUserAccountAsync("testuser");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(entity.Tier, retrieved.Tier);
            Assert.AreEqual(entity.AnalysesUsed, retrieved.AnalysesUsed);
        }
    }
}
