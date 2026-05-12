using Azure.Data.Tables;
using Azure.Storage.Blobs;

using FeedbackFunctions.Services.Storage;

namespace FeedbackFunctions.Services.Reports;

public class ReportStorageService : IReportStorageService
{
    private readonly FeedbackStorageClients _storage;
    private readonly ITableInitializationService _tableInitializationService;

    public ReportStorageService(
        FeedbackStorageClients storage,
        ITableInitializationService tableInitializationService)
    {
        _storage = storage;
        _tableInitializationService = tableInitializationService;
    }

    public BlobContainerClient ReportsContainer => _storage.ReportsContainer;

    public BlobContainerClient ReportsSummaryContainer => _storage.ReportsSummaryContainer;

    public BlobContainerClient WeeklySummariesContainer => _storage.WeeklySummariesContainer;

    public TableClient ReportRequestsTable => _storage.ReportRequestsTable;

    public TableClient UserReportRequestsTable => _storage.UserReportRequestsTable;

    public Task EnsureInitializedAsync() => _tableInitializationService.EnsureReportStorageAsync();
}
