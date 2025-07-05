using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Data.Tables;
using SharedDump.Models.Account;

namespace SharedDump.Services.Account
{
    public interface IUserAccountTableService
    {
        Task<UserAccountEntity?> GetUserAccountAsync(string userId);
        Task UpsertUserAccountAsync(UserAccountEntity entity);
        Task CreateTableIfNotExistsAsync();
        Task<int> ResetAllMonthlyUsageAsync();
    }

    public interface IUsageRecordTableService
    {
        Task AddUsageRecordAsync(UsageRecordEntity entity);
        Task<List<UsageRecordEntity>> GetUsageRecordsForUserAsync(string userId);
        Task CreateTableIfNotExistsAsync();
    }
}
