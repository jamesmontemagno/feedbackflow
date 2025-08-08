using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FeedbackFunctions.Services.Account;
using SharedDump.Models.Account;
using SharedDump.Utils.Account;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Extensions;
using FeedbackFunctions.Attributes;

namespace FeedbackFunctions.Account;

public class AdminApiKeyFunctions
{
    private readonly ILogger<AdminApiKeyFunctions> _logger;
    private readonly IApiKeyService _apiKeyService;
    private readonly IUserAccountService _userAccountService;
    private readonly AuthenticationMiddleware _authMiddleware;

    public AdminApiKeyFunctions(
        ILogger<AdminApiKeyFunctions> logger, 
        IApiKeyService apiKeyService,
        IUserAccountService userAccountService,
        AuthenticationMiddleware authMiddleware)
    {
        _logger = logger;
        _apiKeyService = apiKeyService;
        _userAccountService = userAccountService;
        _authMiddleware = authMiddleware;
    }

    [Function("GetAllApiKeysAdmin")]
    [Authorize]
    public async Task<HttpResponseData> GetAllApiKeysAdmin(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Admin getting all API keys");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Check if user has admin permissions
            var userAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
            if (userAccount == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "User account not found"
                });
                return notFoundResponse;
            }

            if (userAccount.Tier != AccountTier.Admin)
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "Admin access required"
                });
                return forbiddenResponse;
            }

            var apiKeys = await _apiKeyService.GetAllApiKeysAsync();
            
            // Mask user IDs to show only first 4 and last 4 characters
            var maskedApiKeys = apiKeys.Select(key => new
            {
                Key = key.Key[..8] + "..." + key.Key[^4..], // Show first 8 and last 4 chars of API key
                FullKey = key.Key, // Include full key for admin operations
                UserId = MaskUserId(key.UserId),
                key.IsEnabled,
                key.CreatedAt,
                key.LastUsedAt,
                key.Name
            }).ToList();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Data = maskedApiKeys,
                Success = true,
                Message = $"Retrieved {apiKeys.Count} API keys"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all API keys for admin");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrieving API keys");
            return errorResponse;
        }
    }

    [Function("UpdateApiKeyStatusAdmin")]
    [Authorize]
    public async Task<HttpResponseData> UpdateApiKeyStatusAdmin(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Admin updating API key status");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Check if user has admin permissions
            var userAccount = await _userAccountService.GetUserAccountAsync(user!.UserId);
            if (userAccount == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "User account not found"
                });
                return notFoundResponse;
            }

            if (userAccount.Tier != AccountTier.Admin)
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "Admin access required"
                });
                return forbiddenResponse;
            }

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updateRequest = JsonSerializer.Deserialize<UpdateApiKeyStatusRequest>(requestBody, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (updateRequest == null || string.IsNullOrWhiteSpace(updateRequest.ApiKey))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "API key is required"
                });
                return badRequestResponse;
            }

            var updated = await _apiKeyService.UpdateApiKeyStatusAsync(updateRequest.ApiKey, updateRequest.IsEnabled);
            
            var response = req.CreateResponse(updated ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new
            {
                Data = new { ApiKey = updateRequest.ApiKey[..8] + "...", IsEnabled = updateRequest.IsEnabled },
                Success = updated,
                Message = updated ? 
                    $"API key {(updateRequest.IsEnabled ? "enabled" : "disabled")} successfully" : 
                    "API key not found"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error updating API key status");
            return errorResponse;
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

    private class UpdateApiKeyStatusRequest
    {
        public string ApiKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}