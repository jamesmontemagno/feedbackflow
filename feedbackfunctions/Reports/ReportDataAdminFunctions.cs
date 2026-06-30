using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Services.Reports;
using SharedDump.Models.Account;
using SharedDump.Models.Reports;

namespace FeedbackFunctions.Reports;

/// <summary>
/// Admin-only Azure Functions for listing generated reports and downloading their raw data.
/// </summary>
public class ReportDataAdminFunctions
{
    private readonly ILogger<ReportDataAdminFunctions> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;
    private readonly IReportCacheService _cacheService;
    private readonly IReportStorageService _reportStorage;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportDataAdminFunctions(
        ILogger<ReportDataAdminFunctions> logger,
        AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService,
        IReportCacheService cacheService,
        IReportStorageService reportStorage)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
        _cacheService = cacheService;
        _reportStorage = reportStorage;
    }

    /// <summary>
    /// Lists all generated Reddit reports (metadata only) for the admin report-data portal.
    /// Indicates whether raw downloadable data is available for each report.
    /// </summary>
    [Function("GetAllReports")]
    [Authorize]
    public async Task<HttpResponseData> GetAllReports(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            _logger.LogInformation("GetAllReports invoked");

            var reports = await _cacheService.GetReportsAsync("reddit", null);
            await _reportStorage.EnsureInitializedAsync();

            // Determine which reports have raw data available and read the captured totals
            // (stored as blob metadata) so the list reflects the full downloadable dataset.
            var rawDataInfo = new Dictionary<string, (int ThreadCount, int CommentCount)>(StringComparer.OrdinalIgnoreCase);
            await foreach (var blob in _reportStorage.RedditReportDataContainer.GetBlobsAsync(BlobTraits.Metadata))
            {
                var blobId = Path.GetFileNameWithoutExtension(blob.Name);
                var threadCount = 0;
                var commentCount = 0;
                if (blob.Metadata is not null)
                {
                    if (blob.Metadata.TryGetValue("threadCount", out var tc))
                    {
                        int.TryParse(tc, out threadCount);
                    }
                    if (blob.Metadata.TryGetValue("commentCount", out var cc))
                    {
                        int.TryParse(cc, out commentCount);
                    }
                }
                rawDataInfo[blobId] = (threadCount, commentCount);
            }

            var items = reports
                .OrderByDescending(r => r.GeneratedAt)
                .Select(r =>
                {
                    var hasRaw = rawDataInfo.TryGetValue(r.Id.ToString(), out var rawCounts);
                    return new ReportDataListItem
                    {
                        Id = r.Id,
                        Source = r.Source,
                        SubSource = r.SubSource,
                        GeneratedAt = r.GeneratedAt,
                        CutoffDate = r.CutoffDate,
                        // Prefer the full raw-data totals; fall back to the report's analyzed
                        // counts for older reports captured before raw data was stored.
                        ThreadCount = hasRaw && rawCounts.ThreadCount > 0 ? rawCounts.ThreadCount : r.ThreadCount,
                        CommentCount = hasRaw && rawCounts.CommentCount > 0 ? rawCounts.CommentCount : r.CommentCount,
                        HasRawData = hasRaw
                    };
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new ReportDataListResponse { Reports = items });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllReports endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to retrieve reports. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Downloads the raw fetched data (threads + comments + subreddit info) for a report as JSON.
    /// </summary>
    [Function("DownloadReportRawData")]
    [Authorize]
    public async Task<HttpResponseData> DownloadReportRawData(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            var query = QueryHelpers.ParseQuery(req.Url.Query);
            if (!query.TryGetValue("id", out var idValues))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing required query parameter 'id'.");
                return bad;
            }

            var id = idValues.ToString();
            if (string.IsNullOrWhiteSpace(id))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid 'id' value.");
                return bad;
            }

            _logger.LogInformation("DownloadReportRawData invoked for report {ReportId}", id);

            await _reportStorage.EnsureInitializedAsync();
            var blobClient = _reportStorage.RedditReportDataContainer.GetBlobClient($"{id}.json");

            if (!await blobClient.ExistsAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("No raw data found for the specified report.");
                return notFound;
            }

            var content = await blobClient.DownloadContentAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"reddit-report-{id}.json\"");
            await response.WriteBytesAsync(content.Value.Content.ToArray());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DownloadReportRawData endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to download report raw data. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Ensures the caller is an authenticated admin. Returns an error response when not authorized,
    /// otherwise returns null.
    /// </summary>
    private async Task<HttpResponseData?> EnsureAdminAsync(HttpRequestData req)
    {
        var authenticatedUser = await _authMiddleware.GetUserAsync(req);
        if (authenticatedUser is null)
        {
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteStringAsync("Authentication required");
            return unauthorized;
        }

        var userAccount = await _userAccountService.GetUserAccountAsync(authenticatedUser.UserId);
        if (userAccount?.Tier != AccountTier.Admin)
        {
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("Admin access required");
            return forbidden;
        }

        return null;
    }
}
