using System.Collections.Concurrent;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace FeedbackFunctions.Services.Storage;

public class TableInitializationService : ITableInitializationService
{
    private readonly FeedbackStorageClients _storage;
    private readonly ILogger<TableInitializationService> _logger;
    private readonly ConcurrentDictionary<string, Lazy<Task>> _initializers = new(StringComparer.OrdinalIgnoreCase);

    public TableInitializationService(
        FeedbackStorageClients storage,
        ILogger<TableInitializationService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public Task EnsureAccountTablesAsync() =>
        EnsureAsync("accounts", async () =>
        {
            await Task.WhenAll(
                _storage.UserAccountsTable.CreateIfNotExistsAsync(),
                _storage.UsageRecordsTable.CreateIfNotExistsAsync(),
                _storage.ApiKeysTable.CreateIfNotExistsAsync());
        });

    public Task EnsureAuthTablesAsync() =>
        EnsureAsync("auth", () => _storage.AuthUsersTable.CreateIfNotExistsAsync());

    public Task EnsureReportStorageAsync() =>
        EnsureAsync("reports", async () =>
        {
            await Task.WhenAll(
                _storage.ReportRequestsTable.CreateIfNotExistsAsync(),
                _storage.UserReportRequestsTable.CreateIfNotExistsAsync(),
                _storage.ReportsContainer.CreateIfNotExistsAsync(),
                _storage.ReportsSummaryContainer.CreateIfNotExistsAsync(),
                _storage.WeeklySummariesContainer.CreateIfNotExistsAsync());
        });

    public Task EnsureAdminReportConfigsAsync() =>
        EnsureAsync("admin-report-configs", () => _storage.AdminReportConfigsTable.CreateIfNotExistsAsync());

    public Task EnsureSharedAnalysesStorageAsync() =>
        EnsureAsync("shared-analyses", async () =>
        {
            await Task.WhenAll(
                _storage.SharedAnalysesTable.CreateIfNotExistsAsync(),
                _storage.SharedAnalysesContainer.CreateIfNotExistsAsync());
        });

    private Task EnsureAsync(string key, Func<Task> initialize)
    {
        var initializer = _initializers.GetOrAdd(
            key,
            _ => new Lazy<Task>(
                async () =>
                {
                    _logger.LogInformation("Initializing storage scope {Scope}", key);
                    await initialize();
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        return AwaitInitializerAsync(key, initializer);
    }

    private async Task AwaitInitializerAsync(string key, Lazy<Task> initializer)
    {
        try
        {
            await initializer.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Storage scope {Scope} initialization failed and will be retried", key);
            _initializers.TryRemove(new KeyValuePair<string, Lazy<Task>>(key, initializer));
            throw;
        }
    }
}
