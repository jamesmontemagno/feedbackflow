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
using SharedDump.Models.Reddit;
using SharedDump.Models.Reports;
using SharedDump.Services.Interfaces;
using SharedDump.Utils.Account;

namespace FeedbackFunctions.Reports;

/// <summary>
/// Admin-only Azure Functions for creating, listing, downloading, and deleting on-demand
/// subreddit exports (all threads + comments within a date range, stored as JSON blobs).
/// </summary>
public class RedditExportFunctions
{
    private const int MaxWindowDays = 7;
    private const int MaxThreadCap = 500;
    private const int DefaultThreadCap = 200;

    private readonly ILogger<RedditExportFunctions> _logger;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;
    private readonly IReportStorageService _reportStorage;
    private readonly IRedditService _redditService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedditExportFunctions(
        ILogger<RedditExportFunctions> logger,
        AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService,
        IReportStorageService reportStorage,
        IRedditService redditService)
    {
        _logger = logger;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
        _reportStorage = reportStorage;
        _redditService = redditService;
    }

    /// <summary>
    /// Creates a new subreddit export: fetches every thread (and its comments) created in the
    /// requested window (capped), stores the result as a JSON blob, and returns its metadata.
    /// </summary>
    [Function("CreateRedditExport")]
    [Authorize]
    public async Task<HttpResponseData> CreateRedditExport(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            var body = await req.ReadAsStringAsync();
            CreateRedditExportRequest? request = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                request = JsonSerializer.Deserialize<CreateRedditExportRequest>(body, _jsonOptions);
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Subreddit))
            {
                return await BadRequest(req, "A subreddit is required.");
            }

            var subreddit = request.Subreddit.Trim();
            if (subreddit.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
            {
                subreddit = subreddit[2..];
            }
            subreddit = subreddit.Trim('/').Trim();
            if (string.IsNullOrWhiteSpace(subreddit))
            {
                return await BadRequest(req, "A valid subreddit is required.");
            }

            var startDate = request.StartDate.ToUniversalTime();
            var endDate = request.EndDate.ToUniversalTime();

            if (endDate <= startDate)
            {
                return await BadRequest(req, "The end date must be after the start date.");
            }

            if ((endDate - startDate).TotalDays > MaxWindowDays)
            {
                return await BadRequest(req, $"The date range cannot exceed {MaxWindowDays} days.");
            }

            var maxThreads = request.MaxThreads <= 0
                ? DefaultThreadCap
                : Math.Min(request.MaxThreads, MaxThreadCap);

            _logger.LogInformation(
                "CreateRedditExport invoked for r/{Subreddit} from {Start} to {End} (max {Max} threads)",
                subreddit, startDate, endDate, maxThreads);

            // Validate the subreddit exists before doing heavy work.
            if (!await _redditService.CheckSubredditValid(subreddit))
            {
                return await BadRequest(req, $"Subreddit 'r/{subreddit}' was not found or is not accessible.");
            }

            RedditSubredditInfo? subredditInfo = null;
            try
            {
                subredditInfo = await _redditService.GetSubredditInfo(subreddit);
            }
            catch (Exception infoEx)
            {
                _logger.LogWarning(infoEx, "Failed to fetch subreddit info for r/{Subreddit}", subreddit);
            }

            var (basicThreads, limitReached) = await _redditService.GetSubredditThreadsInDateRange(
                subreddit, startDate, endDate, maxThreads);

            _logger.LogInformation(
                "Found {Count} threads in r/{Subreddit} for the window; fetching full comments",
                basicThreads.Count, subreddit);

            // Fetch full threads (with comments) using bounded concurrency to respect rate limits.
            using var throttler = new SemaphoreSlim(5);
            var tasks = basicThreads.Select(async basic =>
            {
                await throttler.WaitAsync();
                try
                {
                    var full = await _redditService.GetThreadWithComments(basic.Id);
                    // GetThreadWithComments does not always set the correct subreddit/created date.
                    full.Subreddit = subreddit;
                    if (full.CreatedUtc == default)
                    {
                        full.CreatedUtc = basic.CreatedUtc;
                    }
                    return full;
                }
                catch (Exception threadEx)
                {
                    _logger.LogWarning(threadEx, "Failed to fetch comments for thread {ThreadId}; skipping", basic.Id);
                    return null;
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            var fullThreads = (await Task.WhenAll(tasks))
                .Where(t => t is not null)
                .Select(t => t!)
                .OrderByDescending(t => t.CreatedUtc)
                .ToList();

            var commentCount = fullThreads.Sum(t => CountComments(t.Comments));

            var export = new RedditSubredditExport
            {
                Subreddit = subreddit,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTimeOffset.UtcNow,
                ThreadCount = fullThreads.Count,
                CommentCount = commentCount,
                ThreadLimitReached = limitReached,
                SubredditInfo = subredditInfo,
                Threads = fullThreads
            };

            await StoreExportAsync(export);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ToListItem(export));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateRedditExport endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to create the subreddit export. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Lists all stored subreddit exports (metadata only), newest first.
    /// </summary>
    [Function("GetRedditExports")]
    [Authorize]
    public async Task<HttpResponseData> GetRedditExports(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            await _reportStorage.EnsureInitializedAsync();

            var items = new List<RedditExportListItem>();
            await foreach (var blob in _reportStorage.RedditExportsContainer.GetBlobsAsync(BlobTraits.Metadata))
            {
                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(blob.Name), out var id))
                {
                    continue;
                }

                var item = new RedditExportListItem { Id = id };
                var meta = blob.Metadata;
                if (meta is not null)
                {
                    if (meta.TryGetValue("subreddit", out var sub)) item.Subreddit = sub;
                    if (meta.TryGetValue("threadCount", out var tc) && int.TryParse(tc, out var tcv)) item.ThreadCount = tcv;
                    if (meta.TryGetValue("commentCount", out var cc) && int.TryParse(cc, out var ccv)) item.CommentCount = ccv;
                    if (meta.TryGetValue("limitReached", out var lr) && bool.TryParse(lr, out var lrv)) item.ThreadLimitReached = lrv;
                    if (meta.TryGetValue("startDate", out var sd) && DateTimeOffset.TryParse(sd, out var sdv)) item.StartDate = sdv;
                    if (meta.TryGetValue("endDate", out var ed) && DateTimeOffset.TryParse(ed, out var edv)) item.EndDate = edv;
                    if (meta.TryGetValue("generatedAt", out var ga) && DateTimeOffset.TryParse(ga, out var gav)) item.GeneratedAt = gav;
                }

                if (item.GeneratedAt == default && blob.Properties.CreatedOn.HasValue)
                {
                    item.GeneratedAt = blob.Properties.CreatedOn.Value;
                }

                items.Add(item);
            }

            var ordered = items.OrderByDescending(i => i.GeneratedAt).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new RedditExportListResponse { Exports = ordered });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRedditExports endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to retrieve exports. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Downloads the full JSON for a stored subreddit export.
    /// </summary>
    [Function("DownloadRedditExport")]
    [Authorize]
    public async Task<HttpResponseData> DownloadRedditExport(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            if (!TryGetId(req, out var id))
            {
                return await BadRequest(req, "Missing or invalid 'id' query parameter.");
            }

            await _reportStorage.EnsureInitializedAsync();
            var blobClient = _reportStorage.RedditExportsContainer.GetBlobClient($"{id}.json");

            if (!await blobClient.ExistsAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("No export found for the specified id.");
                return notFound;
            }

            var content = await blobClient.DownloadContentAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"reddit-export-{id}.json\"");
            await response.WriteBytesAsync(content.Value.Content.ToArray());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DownloadRedditExport endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to download the export. Please try again later.");
            return error;
        }
    }

    /// <summary>
    /// Deletes a stored subreddit export.
    /// </summary>
    [Function("DeleteRedditExport")]
    [Authorize]
    public async Task<HttpResponseData> DeleteRedditExport(
        [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
    {
        try
        {
            var forbidden = await EnsureAdminAsync(req);
            if (forbidden is not null)
            {
                return forbidden;
            }

            if (!TryGetId(req, out var id))
            {
                return await BadRequest(req, "Missing or invalid 'id' query parameter.");
            }

            await _reportStorage.EnsureInitializedAsync();
            var blobClient = _reportStorage.RedditExportsContainer.GetBlobClient($"{id}.json");
            var deleted = await blobClient.DeleteIfExistsAsync();

            if (!deleted.Value)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("No export found for the specified id.");
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true, id });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteRedditExport endpoint");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to delete the export. Please try again later.");
            return error;
        }
    }

    private async Task StoreExportAsync(RedditSubredditExport export)
    {
        await _reportStorage.EnsureInitializedAsync();

        var blobClient = _reportStorage.RedditExportsContainer.GetBlobClient($"{export.Id}.json");
        var json = JsonSerializer.Serialize(export);
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(ms, overwrite: true);

        await blobClient.SetMetadataAsync(new Dictionary<string, string>
        {
            ["subreddit"] = export.Subreddit,
            ["threadCount"] = export.ThreadCount.ToString(),
            ["commentCount"] = export.CommentCount.ToString(),
            ["limitReached"] = export.ThreadLimitReached.ToString(),
            ["startDate"] = export.StartDate.ToString("o"),
            ["endDate"] = export.EndDate.ToString("o"),
            ["generatedAt"] = export.GeneratedAt.ToString("o")
        });

        _logger.LogInformation("Stored Reddit export {ExportId} for r/{Subreddit}", export.Id, export.Subreddit);
    }

    private static RedditExportListItem ToListItem(RedditSubredditExport export) => new()
    {
        Id = export.Id,
        Subreddit = export.Subreddit,
        StartDate = export.StartDate,
        EndDate = export.EndDate,
        GeneratedAt = export.GeneratedAt,
        ThreadCount = export.ThreadCount,
        CommentCount = export.CommentCount,
        ThreadLimitReached = export.ThreadLimitReached
    };

    private static int CountComments(List<RedditCommentModel> comments)
    {
        var count = 0;
        foreach (var comment in comments)
        {
            count++;
            if (comment.Replies is { Count: > 0 })
            {
                count += CountComments(comment.Replies);
            }
        }
        return count;
    }

    private static bool TryGetId(HttpRequestData req, out Guid id)
    {
        id = Guid.Empty;
        var query = QueryHelpers.ParseQuery(req.Url.Query);
        if (!query.TryGetValue("id", out var idValues))
        {
            return false;
        }

        return Guid.TryParse(idValues.ToString(), out id);
    }

    private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var bad = req.CreateResponse(HttpStatusCode.BadRequest);
        await bad.WriteStringAsync(message);
        return bad;
    }

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
        if (userAccount is null || !AccountTierUtils.HasAdminPortalAccess(userAccount.Tier))
        {
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("Admin access required");
            return forbidden;
        }

        return null;
    }
}
