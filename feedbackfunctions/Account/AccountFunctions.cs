using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharedDump.Services.Account;
using SharedDump.Models.Account;
using System.Web;
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions.Account
{
    public class AccountFunctions
    {
        private readonly ILogger<AccountFunctions> _logger;
        private readonly IAccountLimitsService _limitsService;
        private readonly IUsageTrackingService _usageService;
        private readonly AuthenticationMiddleware _authMiddleware;
        private readonly IUserAccountTableService _userAccountService;

        public AccountFunctions(ILogger<AccountFunctions> logger, IAccountLimitsService limitsService, IUsageTrackingService usageService, AuthenticationMiddleware authMiddleware, IUserAccountTableService userAccountService)
        {
            _logger = logger;
            _limitsService = limitsService;
            _usageService = usageService;
            _authMiddleware = authMiddleware;
            _userAccountService = userAccountService;
        }

        [Function("GetUserAccount")]
        [Authorize]
        public async Task<HttpResponseData> GetUserAccount(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting user account");

            // Authenticate the request
            var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
            if (authErrorResponse != null)
                return authErrorResponse;

            var account = await _usageService.GetUserAccountAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(account);
            return response;
        }

        [Function("ValidateUsage")]
        [Authorize]
        public async Task<HttpResponseData> ValidateUsage(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Validating usage");

            // Authenticate the request
            var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
            if (authErrorResponse != null)
                return authErrorResponse;

            var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
            var usageTypeStr = queryParams["usageType"];
            if (!Enum.TryParse<UsageType>(usageTypeStr, out var usageType))
                usageType = UsageType.Analysis;

            var result = await _limitsService.ValidateUsageAsync(user!.UserId, usageType);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }

        [Function("TrackUsage")]
        [Authorize]
        public async Task<HttpResponseData> TrackUsage(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Tracking usage");

            // Authenticate the request
            var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
            if (authErrorResponse != null)
                return authErrorResponse;

            var requestBody = await req.ReadAsStringAsync();
            var trackingRequest = JsonSerializer.Deserialize<TrackUsageRequest>(requestBody ?? "{}");
            
            await _limitsService.TrackUsageAsync(user!.UserId, trackingRequest?.UsageType ?? UsageType.Analysis, trackingRequest?.ResourceId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Usage tracked");
            return response;
        }

        [Function("ResetMonthlyUsage")]
        public async Task RunMonthlyReset([TimerTrigger("0 0 0 1 * *")] TimerInfo timer)
        {
            _logger.LogInformation("Running monthly usage reset");
            
            try
            {
                var resetCount = await _userAccountService.ResetAllMonthlyUsageAsync();
                _logger.LogInformation("Successfully reset monthly usage for {ResetCount} users", resetCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monthly usage reset");
                throw;
            }
        }

        [Function("GetTierLimits")]
        public async Task<HttpResponseData> GetTierLimits(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting tier limits");

            try
            {
                var tiers = new[]
                {
                    new { 
                        Tier = AccountTier.Free,
                        Name = "Free",
                        Description = "Basic analysis, limited usage, no support",
                        Price = "$0/month",
                        Features = new[] { "Basic analysis", "Limited usage", "Community support" },
                        Limits = _limitsService.GetLimitsForTier(AccountTier.Free)
                    },
                    new { 
                        Tier = AccountTier.Pro,
                        Name = "Pro",
                        Description = "Priority processing, increased limits, basic support",
                        Price = "$9.99/month",
                        Features = new[] { "Priority processing", "Increased limits", "Email support", "Advanced features" },
                        Limits = _limitsService.GetLimitsForTier(AccountTier.Pro)
                    },
                    new { 
                        Tier = AccountTier.ProPlus,
                        Name = "Pro+",
                        Description = "Advanced analytics, email notifications, highest limits",
                        Price = "$29.99/month",
                        Features = new[] { "Advanced analytics", "Email notifications", "Highest limits", "Priority support", "Custom integrations" },
                        Limits = _limitsService.GetLimitsForTier(AccountTier.ProPlus)
                    }
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(tiers);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tier limits");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error retrieving tier information");
                return errorResponse;
            }
        }
    }

    public class TrackUsageRequest
    {
        public UsageType UsageType { get; set; }
        public string? ResourceId { get; set; }
    }
}
