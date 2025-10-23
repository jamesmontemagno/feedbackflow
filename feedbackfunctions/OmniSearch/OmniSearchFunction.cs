using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Models.ContentSearch;
using SharedDump.Models.Account;
using FeedbackFunctions.Attributes;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Account;

namespace FeedbackFunctions.OmniSearch;

/// <summary>
/// Azure Function for omni-search across multiple platforms
/// </summary>
public class OmniSearchFunction
{
    private readonly ILogger<OmniSearchFunction> _logger;
    private readonly OmniSearchService _omniSearchService;
    private readonly AuthenticationMiddleware _authMiddleware;
    private readonly IUserAccountService _userAccountService;

    public OmniSearchFunction(
        ILogger<OmniSearchFunction> logger,
        OmniSearchService omniSearchService,
        AuthenticationMiddleware authMiddleware,
        IUserAccountService userAccountService)
    {
        _logger = logger;
        _omniSearchService = omniSearchService;
        _authMiddleware = authMiddleware;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// Omni-search endpoint supporting GET and POST
    /// </summary>
    /// <param name="req">HTTP request with query parameters or JSON body</param>
    /// <returns>Aggregated search results from multiple platforms</returns>
    /// <remarks>
    /// GET Parameters:
    /// - query: Required. Search query string
    /// - platforms: Required. Comma-separated list (youtube,reddit,hackernews,twitter,bluesky)
    /// - fromDate: Optional. ISO 8601 date filter
    /// - toDate: Optional. ISO 8601 date filter
    /// - maxResults: Optional. Max results per platform (default 10, max 50)
    /// - sortMode: Optional. "chronological" (default) or "ranked"
    /// - page: Optional. Page number (default 1)
    /// 
    /// POST Body: OmniSearchRequest JSON
    /// 
    /// Example:
    /// GET /api/OmniSearch?query=dotnet&platforms=youtube,reddit&maxResults=20&sortMode=ranked
    /// </remarks>
    [Function("OmniSearch")]
    [Authorize]
    public async Task<HttpResponseData> OmniSearch(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing omni-search request");

        try
        {
            // Authenticate the request
            var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
            if (authErrorResponse != null)
                return authErrorResponse;

            // Parse request (GET or POST)
            OmniSearchRequest? request = null;

            if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                request = ParseGetRequest(req);
            }
            else if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                request = JsonSerializer.Deserialize(requestBody, FeedbackJsonContext.Default.OmniSearchRequest);
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Query) || request.Platforms.Count == 0)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request. 'query' and 'platforms' are required.");
                return badRequestResponse;
            }

            // Validate platforms
            var validPlatforms = new HashSet<string> { "youtube", "reddit", "hackernews", "twitter", "bluesky" };
            request.Platforms = request.Platforms
                .Where(p => validPlatforms.Contains(p.ToLowerInvariant()))
                .Select(p => p.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (request.Platforms.Count == 0)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("No valid platforms specified. Valid options: youtube, reddit, hackernews, twitter, bluesky");
                return badRequestResponse;
            }

            // Check if user has Twitter in platforms and validate tier access
            if (request.Platforms.Contains("twitter"))
            {
                var userAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
                if (userAccount is null || !SharedDump.Utils.Account.AccountTierUtils.SupportsTwitterAccess(userAccount.Tier))
                {
                    var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                    await forbiddenResponse.WriteAsJsonAsync(new UsageValidationResult
                    {
                        IsWithinLimit = false,
                        ErrorCode = "TIER_UPGRADE_REQUIRED",
                        Message = "Twitter search requires a Pro account or higher.",
                        UsageType = UsageType.FeedQuery,
                        CurrentTier = userAccount?.Tier ?? AccountTier.Free,
                        UpgradeUrl = "/account"
                    });
                    return forbiddenResponse;
                }
            }

            // Validate usage limits - each platform counts as 1 usage
            var platformCount = request.Platforms.Count;
            _logger.LogInformation("Validating usage for {PlatformCount} platforms for user {UserId}", platformCount, user!.UserId);
            
            // Get current usage status
            var usageValidation = await _userAccountService.ValidateUsageAsync(user!.UserId, UsageType.FeedQuery);
            if (!usageValidation.IsWithinLimit)
            {
                var usageValidationResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                await usageValidationResponse.WriteAsJsonAsync(usageValidation);
                return usageValidationResponse;
            }

            // Check if user has enough remaining usage for all platforms
            var remainingUsage = usageValidation.Limit - usageValidation.CurrentUsage;
            if (remainingUsage < platformCount)
            {
                _logger.LogWarning("Insufficient usage credits for user {UserId}. Need {Required}, have {Remaining}", 
                    user!.UserId, platformCount, remainingUsage);
                
                var limitResponse = req.CreateResponse(HttpStatusCode.TooManyRequests);
                await limitResponse.WriteAsJsonAsync(new UsageValidationResult
                {
                    IsWithinLimit = false,
                    ErrorCode = "USAGE_LIMIT_EXCEEDED",
                    Message = $"Insufficient usage credits. You need {platformCount} credits but only have {remainingUsage} remaining.",
                    UsageType = UsageType.FeedQuery,
                    CurrentUsage = usageValidation.CurrentUsage,
                    Limit = usageValidation.Limit,
                    CurrentTier = usageValidation.CurrentTier,
                    ResetDate = usageValidation.ResetDate,
                    UpgradeUrl = "/account"
                });
                return limitResponse;
            }

            _logger.LogInformation("Executing omni-search for user {UserId} - query: '{Query}', platforms: {Platforms}", 
                user!.Email, request.Query, string.Join(", ", request.Platforms));

            // Execute search (pass user for usage tracking)
            var result = await _omniSearchService.SearchAsync(request, user!, _userAccountService, req.FunctionContext.CancellationToken);

            // Return results
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, FeedbackJsonContext.Default.OmniSearchResponse));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing omni-search request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"An error occurred while processing your search: {ex.Message}");
            return errorResponse;
        }
    }

    private OmniSearchRequest ParseGetRequest(HttpRequestData req)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var query = queryParams["query"] ?? "";
        var platformsStr = queryParams["platforms"] ?? "";
        var platforms = platformsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var request = new OmniSearchRequest
        {
            Query = query,
            Platforms = platforms,
            MaxResults = int.TryParse(queryParams["maxResults"], out var maxResults) ? maxResults : 10,
            SortMode = queryParams["sortMode"] ?? "chronological",
            Page = int.TryParse(queryParams["page"], out var page) ? page : 1
        };

        if (DateTimeOffset.TryParse(queryParams["fromDate"], out var fromDate))
            request.FromDate = fromDate;

        if (DateTimeOffset.TryParse(queryParams["toDate"], out var toDate))
            request.ToDate = toDate;

        return request;
    }
}
