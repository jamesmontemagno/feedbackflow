using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using SharedDump.Models.Account;

namespace SharedDump.Services.Account
{
    public class UserAccountTableService : IUserAccountTableService
    {
        private readonly TableClient _tableClient;
        private const string TableName = "UserAccounts";

        public UserAccountTableService(string storageConnectionString)
        {
            _tableClient = new TableClient(storageConnectionString, TableName);
        }

        public async Task CreateTableIfNotExistsAsync()
        {
            await _tableClient.CreateIfNotExistsAsync();
        }

        public async Task<UserAccountEntity?> GetUserAccountAsync(string userId)
        {
            try
            {
                var response = await _tableClient.GetEntityIfExistsAsync<UserAccountEntity>(userId, "account");
                return response.HasValue ? response.Value : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task UpsertUserAccountAsync(UserAccountEntity entity)
        {
            await _tableClient.UpsertEntityAsync(entity);
        }

        public async Task<int> ResetAllMonthlyUsageAsync()
        {
            var resetCount = 0;
            var resetDate = DateTime.UtcNow;

            await foreach (var user in _tableClient.QueryAsync<UserAccountEntity>())
            {
                // Reset usage counters
                user.AnalysesUsed = 0;
                user.FeedQueriesUsed = 0;
                user.LastResetDate = resetDate;
                
                await _tableClient.UpdateEntityAsync(user, user.ETag);
                resetCount++;
            }

            return resetCount;
        }
    }

    public class UsageRecordTableService : IUsageRecordTableService
    {
        private readonly TableClient _tableClient;
        private const string TableName = "UsageRecords";

        public UsageRecordTableService(string storageConnectionString)
        {
            _tableClient = new TableClient(storageConnectionString, TableName);
        }

        public async Task CreateTableIfNotExistsAsync()
        {
            await _tableClient.CreateIfNotExistsAsync();
        }

        public async Task AddUsageRecordAsync(UsageRecordEntity entity)
        {
            await _tableClient.AddEntityAsync(entity);
        }

        public async Task<List<UsageRecordEntity>> GetUsageRecordsForUserAsync(string userId)
        {
            var results = new List<UsageRecordEntity>();
            await foreach (var entity in _tableClient.QueryAsync<UsageRecordEntity>(e => e.PartitionKey == userId))
            {
                results.Add(entity);
            }
            return results;
        }
    }
}
