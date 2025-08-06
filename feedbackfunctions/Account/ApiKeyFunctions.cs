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

public class ApiKeyFunctions
{
    private readonly ILogger<ApiKeyFunctions> _logger;
    private readonly IApiKeyService _apiKeyService;
    private readonly IUserAccountService _userAccountService;
    private readonly AuthenticationMiddleware _authMiddleware;

    public ApiKeyFunctions(
        ILogger<ApiKeyFunctions> logger, 
        IApiKeyService apiKeyService,
        IUserAccountService userAccountService,
        AuthenticationMiddleware authMiddleware)
    {
        _logger = logger;
        _apiKeyService = apiKeyService;
        _userAccountService = userAccountService;
        _authMiddleware = authMiddleware;
    }

    [Function("GetUserApiKey")]
    [Authorize]
    public async Task<HttpResponseData> GetUserApiKey(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Getting user API key");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Check if user has permission to use API keys
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

            if (!AccountTierUtils.SupportsApiKeyGeneration(userAccount.Tier))
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "API key generation is only available for Pro+ and higher tier accounts"
                });
                return forbiddenResponse;
            }

            var apiKey = await _apiKeyService.GetApiKeyByUserIdAsync(user.UserId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Data = apiKey,
                Success = true,
                Message = apiKey != null ? "API key retrieved successfully" : "No API key found for user"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key for user {UserId}", user?.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error retrieving API key");
            return errorResponse;
        }
    }

    [Function("CreateUserApiKey")]
    [Authorize]
    public async Task<HttpResponseData> CreateUserApiKey(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Creating user API key");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Check if user has permission to use API keys
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

            if (!AccountTierUtils.SupportsApiKeyGeneration(userAccount.Tier))
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "API key generation is only available for Pro+ and higher tier accounts"
                });
                return forbiddenResponse;
            }

            // Parse request body for optional name
            string? keyName = null;
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    var requestData = JsonSerializer.Deserialize<CreateApiKeyRequest>(requestBody, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    keyName = requestData?.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse request body for API key name, using default");
            }

            var apiKey = await _apiKeyService.CreateApiKeyAsync(user.UserId, keyName);
            
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                Data = apiKey,
                Success = true,
                Message = "API key created successfully. Note: The key is disabled by default and requires admin approval."
            });
            return response;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "User {UserId} attempted to create duplicate API key", user?.UserId);
            var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
            await conflictResponse.WriteAsJsonAsync(new
            {
                Data = (object?)null,
                Success = false,
                Message = ex.Message
            });
            return conflictResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key for user {UserId}", user?.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error creating API key");
            return errorResponse;
        }
    }

    [Function("DeleteUserApiKey")]
    [Authorize]
    public async Task<HttpResponseData> DeleteUserApiKey(
        [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
    {
        _logger.LogInformation("Deleting user API key");

        // Authenticate the request
        var (user, authErrorResponse) = await req.AuthenticateAsync(_authMiddleware);
        if (authErrorResponse != null)
            return authErrorResponse;

        try
        {
            // Check if user has permission to use API keys
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

            if (!AccountTierUtils.SupportsApiKeyGeneration(userAccount.Tier))
            {
                var forbiddenResponse = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbiddenResponse.WriteAsJsonAsync(new
                {
                    Data = (object?)null,
                    Success = false,
                    Message = "API key management is only available for Pro+ and higher tier accounts"
                });
                return forbiddenResponse;
            }

            var deleted = await _apiKeyService.DeleteApiKeyAsync(user.UserId);
            
            var response = req.CreateResponse(deleted ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new
            {
                Data = (object?)null,
                Success = deleted,
                Message = deleted ? "API key deleted successfully" : "No API key found to delete"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API key for user {UserId}", user?.UserId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Error deleting API key");
            return errorResponse;
        }
    }

    private class CreateApiKeyRequest
    {
        public string? Name { get; set; }
    }
}