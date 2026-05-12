using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace FeedbackFunctions.Services.Reports;

public interface IReportStorageService
{
    BlobContainerClient ReportsContainer { get; }

    BlobContainerClient ReportsSummaryContainer { get; }

    BlobContainerClient WeeklySummariesContainer { get; }

    TableClient ReportRequestsTable { get; }

    TableClient UserReportRequestsTable { get; }

    Task EnsureInitializedAsync();
}
