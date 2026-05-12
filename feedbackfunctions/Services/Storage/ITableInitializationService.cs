namespace FeedbackFunctions.Services.Storage;

public interface ITableInitializationService
{
    Task EnsureAccountTablesAsync();

    Task EnsureAuthTablesAsync();

    Task EnsureReportStorageAsync();

    Task EnsureAdminReportConfigsAsync();

    Task EnsureSharedAnalysesStorageAsync();
}
