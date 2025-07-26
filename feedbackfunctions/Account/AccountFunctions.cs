using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using System.Web;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions.Account
{
    public class AccountFunctions
    {
        private readonly ILogger<AccountFunctions> _logger;
        private readonly IUserAccountService _userAccountService;
        private readonly AuthenticationMiddleware _authMiddleware;

        public AccountFunctions(ILogger<AccountFunctions> logger, IUserAccountService userAccountService, AuthenticationMiddleware authMiddleware)
        {
            _logger = logger;
            _userAccountService = userAccountService;
            _authMiddleware = authMiddleware;
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

            try
            {
                var account = await _userAccountService.GetUserAccountAsync(user!.UserId);
                
                // If account doesn't exist, return error - accounts should only be created during registration
                if (account == null)
                {
                    _logger.LogWarning("User account not found for user {UserId}. User must register first.", user!.UserId);
                    
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteAsJsonAsync(new
                    {
                        Data = (object?)null,
                        Success = false,
                        Message = "User account not found. Please register first."
                    });
                    return notFoundResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Data = account,
                    Success = true,
                    Message = "User account retrieved successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating user account for user {UserId}", user?.UserId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error retrieving user account");
                return errorResponse;
            }
        }

        /// <summary>
        /// Update the user's preferred email address
        /// </summary>
        /// <param name="req">HTTP request</param>
        /// <returns>HTTP response with success status</returns>
        [Function("UpdatePreferredEmail")]
        [Authorize]

        public async Task<HttpResponseData> UpdatePreferredEmailAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("UpdatePreferredEmail function triggered");

                // Get authenticated user from middleware
                var authenticatedUser = await _authMiddleware.GetUserAsync(req);
                if (authenticatedUser == null)
                {
                    _logger.LogWarning("No authenticated user found for UpdatePreferredEmail request");
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorizedResponse.WriteStringAsync("User authentication required");
                    return unauthorizedResponse;
                }

                // Read the request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Request body is required");
                    return badRequestResponse;
                }

                // Parse the preferred email from the request
                var requestData = JsonSerializer.Deserialize<UpdatePreferredEmailRequest>(requestBody, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (requestData == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid request format");
                    return badRequestResponse;
                }

                // Validate email format if provided
                if (!string.IsNullOrWhiteSpace(requestData.PreferredEmail))
                {
                    if (!IsValidEmail(requestData.PreferredEmail))
                    {
                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequestResponse.WriteStringAsync("Invalid email format");
                        return badRequestResponse;
                    }
                }

                // Update the user's preferred email in UserAccount
                var userAccount = await _userAccountService.GetUserAccountAsync(authenticatedUser.UserId);
                if (userAccount == null)
                {
                    _logger.LogError("User account not found for user {UserId}", authenticatedUser.UserId);
                    var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await errorResponse.WriteStringAsync("User account not found");
                    return errorResponse;
                }

                // Update the preferred email
                userAccount.PreferredEmail = requestData.PreferredEmail ?? string.Empty;
                await _userAccountService.UpsertUserAccountAsync(userAccount);

                _logger.LogInformation("Successfully updated preferred email for user {UserId}", authenticatedUser.UserId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { Success = true, Message = "Preferred email updated successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdatePreferredEmail function");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Internal server error occurred while updating preferred email");
                return errorResponse;
            }
        }

        /// <summary>
        /// Simple email validation
        /// </summary>
        /// <param name="email">Email to validate</param>
        /// <returns>True if email format is valid</returns>
        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Request model for updating preferred email
        /// </summary>
        private class UpdatePreferredEmailRequest
        {
            public string? PreferredEmail { get; set; }
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
                var queryParams = HttpUtility.ParseQueryString(req.Url.Query);
                var tierStr = queryParams["tier"];
                
                // If a specific tier is requested, return just that tier's limits
                if (!string.IsNullOrEmpty(tierStr) && int.TryParse(tierStr, out var tierInt) && Enum.IsDefined(typeof(AccountTier), tierInt))
                {
                    var requestedTier = (AccountTier)tierInt;
                    var limits = _userAccountService.GetLimitsForTier(requestedTier);
                    
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new
                    {
                        Data = limits,
                        Success = true,
                        Message = $"Limits for {requestedTier} tier retrieved successfully"
                    });
                    return response;
                }

                // Otherwise, return all tier information
                var tiers = new[]
                {
                    new { 
                        Tier = AccountTier.Free,
                        Name = "Free",
                        Description = "Basic analysis, limited usage, no support",
                        Price = "$0/month",
                        Features = new[] { "Basic analysis", "Limited usage", "Community support" },
                        Limits = _userAccountService.GetLimitsForTier(AccountTier.Free)
                    },
                    new { 
                        Tier = AccountTier.Pro,
                        Name = "Pro",
                        Description = "Priority processing, increased limits, basic support",
                        Price = "$9.99/month",
                        Features = new[] { "Priority processing", "Increased limits", "Email support", "Advanced features", "📧 Email notifications" },
                        Limits = _userAccountService.GetLimitsForTier(AccountTier.Pro)
                    },
                    new { 
                        Tier = AccountTier.ProPlus,
                        Name = "Pro+",
                        Description = "Advanced analytics, email notifications, highest limits",
                        Price = "$29.99/month",
                        Features = new[] { "Advanced analytics", "📧 Email notifications", "Highest limits", "Priority support", "Custom integrations" },
                        Limits = _userAccountService.GetLimitsForTier(AccountTier.ProPlus)
                    }
                };

                var allTiersResponse = req.CreateResponse(HttpStatusCode.OK);
                await allTiersResponse.WriteAsJsonAsync(new
                {
                    Data = tiers,
                    Success = true,
                    Message = "All tier information retrieved successfully"
                });
                return allTiersResponse;
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
}
