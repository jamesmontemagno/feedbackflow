using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions.Account;

/// <summary>
/// Administrative functions for viewing and modifying user account tiers.
/// Admins may set tiers only to Free, Pro, or ProPlus. No PII (email, name) is ever returned.
/// Routes:
/// - GET /api/GetAllUserTiersAdmin
/// - POST /api/UpdateUserTierAdmin
/// </summary>
public class AdminUserTierFunctions
{
    private readonly ILogger<AdminUserTierFunctions> _logger;
    private readonly IUserAccountService _userAccountService;
    private readonly AuthenticationMiddleware _authMiddleware;

    private static readonly AccountTier[] AllowedTargetTiers =
    [
        AccountTier.Free,
        AccountTier.Pro,
        AccountTier.ProPlus
    ];

    public AdminUserTierFunctions(
        ILogger<AdminUserTierFunctions> logger,
        IUserAccountService userAccountService,
        AuthenticationMiddleware authMiddleware)
    {
        _logger = logger;
        _userAccountService = userAccountService;
        _authMiddleware = authMiddleware;
    }

    [Function("GetAllUserTiersAdmin")]
    [Authorize]
    public async Task<HttpResponseData> GetAllUserTiersAdmin(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Admin listing user tiers");

        // Authenticate request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var adminAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
            if (adminAccount?.Tier != AccountTier.Admin)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "Admin access required" });
                return forbidden;
            }

            var all = await _userAccountService.GetAllUserAccountsAsync();

            // Project & mask (exclude admin & super users to avoid accidental edits + internal accounts)
            var result = all
                .Where(a => a.Tier != AccountTier.Admin && a.Tier != AccountTier.SuperUser)
                .Select(a => new UserTierAdminView
                {
                    UserId = a.UserId, // full id (non-PII GUID) used for updates
                    MaskedUserId = MaskUserId(a.UserId),
                    Tier = a.Tier,
                    AnalysesUsed = a.AnalysesUsed,
                    ActiveReports = a.ActiveReports,
                    FeedQueriesUsed = a.FeedQueriesUsed,
                    ApiUsed = a.ApiUsed,
                    CreatedAt = a.CreatedAt
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Data = result,
                Success = true,
                Message = $"Retrieved {result.Count} user tiers"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing user tiers for admin");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Error retrieving user tiers");
            return error;
        }
    }

    private class UpdateUserTierRequest
    {
        public string UserId { get; set; } = string.Empty;
        public AccountTier Tier { get; set; }
    }

    [Function("UpdateUserTierAdmin")]
    [Authorize]
    public async Task<HttpResponseData> UpdateUserTierAdmin(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Admin updating user tier");

        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            var adminAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
            if (adminAccount?.Tier != AccountTier.Admin)
            {
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "Admin access required" });
                return forbidden;
            }

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var updateReq = JsonSerializer.Deserialize<UpdateUserTierRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (updateReq == null || string.IsNullOrWhiteSpace(updateReq.UserId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "UserId required" });
                return bad;
            }

            if (!AllowedTargetTiers.Contains(updateReq.Tier))
            {
                var badTier = req.CreateResponse(HttpStatusCode.BadRequest);
                await badTier.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "Tier not allowed" });
                return badTier;
            }

            var account = await _userAccountService.GetUserAccountAsync(updateReq.UserId);
            if (account == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "User not found" });
                return notFound;
            }

            if (account.Tier == AccountTier.Admin || account.Tier == AccountTier.SuperUser)
            {
                var forbiddenTarget = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenTarget.WriteAsJsonAsync(new { Data = (object?)null, Success = false, Message = "Cannot modify this account" });
                return forbiddenTarget;
            }

            // Update and persist
            account.Tier = updateReq.Tier;
            await _userAccountService.UpsertUserAccountAsync(account);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Data = new { UserId = account.UserId, Tier = account.Tier.ToString(), MaskedUserId = MaskUserId(account.UserId) },
                Success = true,
                Message = "User tier updated successfully"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user tier");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Error updating user tier");
            return error;
        }
    }

    private static string MaskUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "****";
        if (userId.Length <= 8)
            return userId[..4] + "****";
        return userId[..4] + "****" + userId[^4..];
    }

    public class UserTierAdminView
    {
        public string UserId { get; set; } = string.Empty; // full id for updates
        public string MaskedUserId { get; set; } = string.Empty; // display only
        public AccountTier Tier { get; set; }
        public int AnalysesUsed { get; set; }
        public int FeedQueriesUsed { get; set; }
        public int ActiveReports { get; set; }
        public int ApiUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
