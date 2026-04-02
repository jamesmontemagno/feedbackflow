using System.Threading;

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using FeedbackFunctions.Services.Storage;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NSubstitute;

namespace FeedbackFlow.Tests;

[TestClass]
public class TableInitializationServiceTests
{
    [TestMethod]
    public async Task EnsureAccountTablesAsync_WhenCalledConcurrently_InitializesEachTableOnce()
    {
        var userAccountsTable = Substitute.For<TableClient>();
        var usageRecordsTable = Substitute.For<TableClient>();
        var apiKeysTable = Substitute.For<TableClient>();

        var storage = CreateStorage(
            userAccountsTable: userAccountsTable,
            usageRecordsTable: usageRecordsTable,
            apiKeysTable: apiKeysTable);

        var service = new TableInitializationService(
            storage,
            Substitute.For<ILogger<TableInitializationService>>());

        var userAccountsCalls = 0;
        var usageRecordsCalls = 0;
        var apiKeysCalls = 0;

        userAccountsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref userAccountsCalls);
                return Task.FromResult<Response<TableItem>>(null!);
            });

        usageRecordsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref usageRecordsCalls);
                return Task.FromResult<Response<TableItem>>(null!);
            });

        apiKeysTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref apiKeysCalls);
                return Task.FromResult<Response<TableItem>>(null!);
            });

        await Task.WhenAll(
            service.EnsureAccountTablesAsync(),
            service.EnsureAccountTablesAsync(),
            service.EnsureAccountTablesAsync());

        Assert.AreEqual(1, userAccountsCalls);
        Assert.AreEqual(1, usageRecordsCalls);
        Assert.AreEqual(1, apiKeysCalls);
    }

    [TestMethod]
    public async Task EnsureReportStorageAsync_WhenCalledMultipleTimes_InitializesAllResourcesOnce()
    {
        var reportRequestsTable = Substitute.For<TableClient>();
        var userReportRequestsTable = Substitute.For<TableClient>();
        var reportsContainer = Substitute.For<BlobContainerClient>();
        var reportsSummaryContainer = Substitute.For<BlobContainerClient>();
        var weeklySummariesContainer = Substitute.For<BlobContainerClient>();

        var storage = CreateStorage(
            reportRequestsTable: reportRequestsTable,
            userReportRequestsTable: userReportRequestsTable,
            reportsContainer: reportsContainer,
            reportsSummaryContainer: reportsSummaryContainer,
            weeklySummariesContainer: weeklySummariesContainer);

        var service = new TableInitializationService(
            storage,
            Substitute.For<ILogger<TableInitializationService>>());

        var reportRequestsCalls = 0;
        var userReportRequestsCalls = 0;
        var reportsContainerCalls = 0;
        var reportsSummaryCalls = 0;
        var weeklySummariesCalls = 0;

        reportRequestsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref reportRequestsCalls);
                return Task.FromResult<Response<TableItem>>(null!);
            });

        userReportRequestsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref userReportRequestsCalls);
                return Task.FromResult<Response<TableItem>>(null!);
            });

        reportsContainer
            .CreateIfNotExistsAsync(Arg.Any<PublicAccessType>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<BlobContainerEncryptionScopeOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref reportsContainerCalls);
                return Task.FromResult<Response<Azure.Storage.Blobs.Models.BlobContainerInfo>>(null!);
            });

        reportsSummaryContainer
            .CreateIfNotExistsAsync(Arg.Any<PublicAccessType>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<BlobContainerEncryptionScopeOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref reportsSummaryCalls);
                return Task.FromResult<Response<Azure.Storage.Blobs.Models.BlobContainerInfo>>(null!);
            });

        weeklySummariesContainer
            .CreateIfNotExistsAsync(Arg.Any<PublicAccessType>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<BlobContainerEncryptionScopeOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref weeklySummariesCalls);
                return Task.FromResult<Response<Azure.Storage.Blobs.Models.BlobContainerInfo>>(null!);
            });

        await service.EnsureReportStorageAsync();
        await service.EnsureReportStorageAsync();

        Assert.AreEqual(1, reportRequestsCalls);
        Assert.AreEqual(1, userReportRequestsCalls);
        Assert.AreEqual(1, reportsContainerCalls);
        Assert.AreEqual(1, reportsSummaryCalls);
        Assert.AreEqual(1, weeklySummariesCalls);
    }

    [TestMethod]
    public async Task EnsureAccountTablesAsync_WhenInitializationFails_RetriesOnNextCall()
    {
        var userAccountsTable = Substitute.For<TableClient>();
        var usageRecordsTable = Substitute.For<TableClient>();
        var apiKeysTable = Substitute.For<TableClient>();

        var storage = CreateStorage(
            userAccountsTable: userAccountsTable,
            usageRecordsTable: usageRecordsTable,
            apiKeysTable: apiKeysTable);

        var service = new TableInitializationService(
            storage,
            Substitute.For<ILogger<TableInitializationService>>());

        var userAccountsCalls = 0;

        userAccountsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var callNumber = Interlocked.Increment(ref userAccountsCalls);
                return callNumber == 1
                    ? Task.FromException<Response<TableItem>>(new InvalidOperationException("Transient failure"))
                    : Task.FromResult<Response<TableItem>>(null!);
            });

        usageRecordsTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Response<TableItem>>(null!));

        apiKeysTable
            .CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Response<TableItem>>(null!));

        var failed = false;
        try
        {
            await service.EnsureAccountTablesAsync();
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }

        Assert.IsTrue(failed);
        await service.EnsureAccountTablesAsync();

        Assert.AreEqual(2, userAccountsCalls);
    }

    private static FeedbackStorageClients CreateStorage(
        TableClient? userAccountsTable = null,
        TableClient? usageRecordsTable = null,
        TableClient? apiKeysTable = null,
        TableClient? authUsersTable = null,
        TableClient? reportRequestsTable = null,
        TableClient? userReportRequestsTable = null,
        TableClient? adminReportConfigsTable = null,
        TableClient? sharedAnalysesTable = null,
        BlobContainerClient? reportsContainer = null,
        BlobContainerClient? reportsSummaryContainer = null,
        BlobContainerClient? weeklySummariesContainer = null,
        BlobContainerClient? sharedAnalysesContainer = null)
    {
        return new FeedbackStorageClients(
            blobServiceClient: null,
            tableServiceClient: null,
            reportsContainer: reportsContainer ?? Substitute.For<BlobContainerClient>(),
            reportsSummaryContainer: reportsSummaryContainer ?? Substitute.For<BlobContainerClient>(),
            weeklySummariesContainer: weeklySummariesContainer ?? Substitute.For<BlobContainerClient>(),
            sharedAnalysesContainer: sharedAnalysesContainer ?? Substitute.For<BlobContainerClient>(),
            userAccountsTable: userAccountsTable ?? Substitute.For<TableClient>(),
            usageRecordsTable: usageRecordsTable ?? Substitute.For<TableClient>(),
            apiKeysTable: apiKeysTable ?? Substitute.For<TableClient>(),
            authUsersTable: authUsersTable ?? Substitute.For<TableClient>(),
            reportRequestsTable: reportRequestsTable ?? Substitute.For<TableClient>(),
            userReportRequestsTable: userReportRequestsTable ?? Substitute.For<TableClient>(),
            adminReportConfigsTable: adminReportConfigsTable ?? Substitute.For<TableClient>(),
            sharedAnalysesTable: sharedAnalysesTable ?? Substitute.For<TableClient>());
    }
}
