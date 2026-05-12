using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace FeedbackFunctions.Services.Storage;

public class FeedbackStorageClients
{
    public FeedbackStorageClients(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient)
        : this(
            blobServiceClient,
            tableServiceClient,
            blobServiceClient.GetBlobContainerClient(StorageNames.ReportsContainer),
            blobServiceClient.GetBlobContainerClient(StorageNames.ReportsSummaryContainer),
            blobServiceClient.GetBlobContainerClient(StorageNames.WeeklySummariesContainer),
            blobServiceClient.GetBlobContainerClient(StorageNames.SharedAnalysesContainer),
            tableServiceClient.GetTableClient(StorageNames.UserAccountsTable),
            tableServiceClient.GetTableClient(StorageNames.UsageRecordsTable),
            tableServiceClient.GetTableClient(StorageNames.ApiKeysTable),
            tableServiceClient.GetTableClient(StorageNames.AuthUsersTable),
            tableServiceClient.GetTableClient(StorageNames.ReportRequestsTable),
            tableServiceClient.GetTableClient(StorageNames.UserReportRequestsTable),
            tableServiceClient.GetTableClient(StorageNames.AdminReportConfigsTable),
            tableServiceClient.GetTableClient(StorageNames.SharedAnalysesTable))
    {
    }

    public FeedbackStorageClients(
        BlobServiceClient? blobServiceClient,
        TableServiceClient? tableServiceClient,
        BlobContainerClient reportsContainer,
        BlobContainerClient reportsSummaryContainer,
        BlobContainerClient weeklySummariesContainer,
        BlobContainerClient sharedAnalysesContainer,
        TableClient userAccountsTable,
        TableClient usageRecordsTable,
        TableClient apiKeysTable,
        TableClient authUsersTable,
        TableClient reportRequestsTable,
        TableClient userReportRequestsTable,
        TableClient adminReportConfigsTable,
        TableClient sharedAnalysesTable)
    {
        BlobServiceClient = blobServiceClient;
        TableServiceClient = tableServiceClient;
        ReportsContainer = reportsContainer;
        ReportsSummaryContainer = reportsSummaryContainer;
        WeeklySummariesContainer = weeklySummariesContainer;
        SharedAnalysesContainer = sharedAnalysesContainer;
        UserAccountsTable = userAccountsTable;
        UsageRecordsTable = usageRecordsTable;
        ApiKeysTable = apiKeysTable;
        AuthUsersTable = authUsersTable;
        ReportRequestsTable = reportRequestsTable;
        UserReportRequestsTable = userReportRequestsTable;
        AdminReportConfigsTable = adminReportConfigsTable;
        SharedAnalysesTable = sharedAnalysesTable;
    }

    public BlobServiceClient? BlobServiceClient { get; }

    public TableServiceClient? TableServiceClient { get; }

    public BlobContainerClient ReportsContainer { get; }

    public BlobContainerClient ReportsSummaryContainer { get; }

    public BlobContainerClient WeeklySummariesContainer { get; }

    public BlobContainerClient SharedAnalysesContainer { get; }

    public TableClient UserAccountsTable { get; }

    public TableClient UsageRecordsTable { get; }

    public TableClient ApiKeysTable { get; }

    public TableClient AuthUsersTable { get; }

    public TableClient ReportRequestsTable { get; }

    public TableClient UserReportRequestsTable { get; }

    public TableClient AdminReportConfigsTable { get; }

    public TableClient SharedAnalysesTable { get; }
}
