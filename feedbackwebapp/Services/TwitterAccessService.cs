using System.Text.Json;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Models.Account;
using SharedDump.Utils.Account;
using SharedDump.Utils;

namespace FeedbackWebApp.Services;

/// <summary>
/// Service for checking Twitter/X access restrictions based on user account tier
/// </summary>
public interface ITwitterAccessService
{
    /// <summary>
    /// Checks if the current user has access to Twitter/X features
    /// </summary>
    /// <param name="urls">List of URLs to check for Twitter/X links</param>
    /// <returns>True if user has access, false otherwise</returns>
    Task<(bool hasAccess, string? errorMessage)> CheckTwitterAccessAsync(IEnumerable<string> urls);
}

/// <summary>
/// Implementation of Twitter/X access checking service
/// </summary>
public class TwitterAccessService : ITwitterAccessService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationHeaderService _authHeaderService;

    public TwitterAccessService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAuthenticationHeaderService authHeaderService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _authHeaderService = authHeaderService;
    }

    public async Task<(bool hasAccess, string? errorMessage)> CheckTwitterAccessAsync(IEnumerable<string> urls)
    {
        // Check if any of the URLs are Twitter/X URLs
        var hasTwitterUrls = urls.Any(url => 
            !string.IsNullOrWhiteSpace(url) && UrlParsing.IsTwitterUrl(url));

        if (!hasTwitterUrls)
        {
            return (true, null); // No Twitter URLs, access is allowed
        }

        try
        {
            // Get user account information
            var userAccount = await GetUserAccountAsync();
            
            // If we can't get account info, allow access and let backend handle validation
            if (userAccount == null)
            {
                return (true, null);
            }

            // Check if user's tier supports Twitter/X access
            if (!AccountTierUtils.SupportsTwitterAccess(userAccount.Tier))
            {
                var tierName = AccountTierUtils.GetTierName(userAccount.Tier);
                var requiredTier = AccountTierUtils.GetTierName(AccountTierUtils.GetMinimumTierForTwitterAccess());
                var errorMessage = $"Twitter/X access requires a {requiredTier} plan or higher. Your current plan is {tierName}. Please upgrade your account to access Twitter/X features.";
                return (false, errorMessage);
            }

            return (true, null);
        }
        catch
        {
            // If there's an error checking access, allow it and let backend handle validation
            return (true, null);
        }
    }

    /// <summary>
    /// Gets the current user's account information
    /// </summary>
    private async Task<UserAccount?> GetUserAccountAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"]
                ?? throw new InvalidOperationException("Base URL not configured");

            var accountCode = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions API code not configured");

            var getUserAccountUrl = $"{baseUrl}/api/GetUserAccount?code={Uri.EscapeDataString(accountCode)}";
            var accountRequest = new HttpRequestMessage(HttpMethod.Get, getUserAccountUrl);
            await _authHeaderService.AddAuthenticationHeadersAsync(accountRequest);

            var client = _httpClientFactory.CreateClient();
            var accountResponse = await client.SendAsync(accountRequest);
            
            if (!accountResponse.IsSuccessStatusCode)
            {
                return null; // Return null if account not found
            }

            var accountContent = await accountResponse.Content.ReadAsStringAsync();
            var accountResult = JsonSerializer.Deserialize<ApiResponse<UserAccount>>(accountContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return accountResult?.Data;
        }
        catch
        {
            // If we can't get account info, return null
            return null;
        }
    }

    /// <summary>
    /// API response wrapper
    /// </summary>
    private class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
