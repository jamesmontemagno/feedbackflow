using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Services;
using SharedDump.Models;

namespace FeedbackFunctions.Functions;

public class MastodonFunctions
{
    private readonly IMastodonService _mastodonService;
    private readonly ILogger<MastodonFunctions> _logger;

    public MastodonFunctions(IMastodonService mastodonService, ILogger<MastodonFunctions> logger)
    {
        _mastodonService = mastodonService;
        _logger = logger;
    }

    [Function("MastodonSearch")]
    public async Task<HttpResponseData> SearchAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mastodon/search")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("q") ?? string.Empty;
        var instance = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("instance") ?? "mastodon.social";
        try
        {
            var results = await _mastodonService.SearchAsync(query, instance);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(results);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Mastodon");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error searching Mastodon");
            return errorResponse;
        }
    }

    [Function("MastodonThread")]
    public async Task<HttpResponseData> GetThreadAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mastodon/thread")] HttpRequestData req)
    {
        var url = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("url") ?? string.Empty;
        var instance = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("instance") ?? "mastodon.social";
        try
        {
            var thread = await _mastodonService.GetThreadAsync(url, instance);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(thread);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Mastodon thread");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error fetching Mastodon thread");
            return errorResponse;
        }
    }
}
