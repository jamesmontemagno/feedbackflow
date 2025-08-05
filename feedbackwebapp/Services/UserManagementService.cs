using System.Net;
using System.Text.Json;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

/// <summary>
/// Service for managing user accounts and registration
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Register or update the current user in the system
    /// </summary>
    /// <param name="preferredEmail">Optional preferred email address to use for registration</param>
    /// <returns>Result of the registration operation</returns>
    Task<RequestResult> RegisterCurrentUserAsync(string? preferredEmail = null);
    
    /// <summary>
    /// Delete the current user's account (deactivates the account for data integrity)
    /// </summary>
    /// <returns>Result of the deletion operation</returns>
    Task<RequestResult> DeleteCurrentUserAsync();
    
    /// <summary>
    /// Get current user information from the backend
    /// </summary>
    /// <returns>User information or null if not found</returns>
    Task<UserInfo?> GetCurrentUserInfoAsync();
    
    /// <summary>
    /// Update the user's preferred email address
    /// </summary>
    /// <param name="preferredEmail">The new preferred email address</param>
    /// <returns>Result of the update operation</returns>
    Task<RequestResult> UpdatePreferredEmailAsync(string? preferredEmail);
    
    /// <summary>
    /// Update the user's email notification settings
    /// </summary>
    /// <param name="emailNotificationsEnabled">Whether email notifications are enabled</param>
    /// <param name="emailFrequency">Email notification frequency preference</param>
    /// <returns>Result of the update operation</returns>
    Task<RequestResult> UpdateEmailNotificationSettingsAsync(bool emailNotificationsEnabled, SharedDump.Models.Account.EmailReportFrequency emailFrequency);
}

/// <summary>
/// User information returned from the backend
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PreferredEmail { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AuthProvider { get; set; } = string.Empty;
    public string? ProviderUserId { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}

/// <summary>
/// Implementation of user management service
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UserManagementService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAuthenticationHeaderService authHeaderService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _authHeaderService = authHeaderService;
    }

    /// <inheritdoc />
    public async Task<RequestResult> RegisterCurrentUserAsync(string? preferredEmail = null)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions key not configured");

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/RegisterUser?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            // Add preferred email in request body if provided
            if (!string.IsNullOrEmpty(preferredEmail))
            {
                var requestBody = new { PreferredEmail = preferredEmail };
                var jsonContent = JsonSerializer.Serialize(requestBody);
                requestMessage.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                Console.WriteLine($"Sending registration request with preferred email: {preferredEmail}");
                Console.WriteLine($"Request body JSON: {jsonContent}");
            }
            else
            {
                Console.WriteLine("No preferred email provided - sending request without body");
            }

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (response.IsSuccessStatusCode)
            {
                return RequestResult.CreateSuccess();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = string.IsNullOrWhiteSpace(errorContent) 
                ? $"Registration failed with status {response.StatusCode}" 
                : errorContent;
            
            return RequestResult.CreateError(errorMessage, response.StatusCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering user: {ex.Message}");
            return RequestResult.CreateError($"Network error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<RequestResult> DeleteCurrentUserAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions key not configured");

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/api/DeleteUser?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (response.IsSuccessStatusCode)
            {
                return RequestResult.CreateSuccess();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = string.IsNullOrWhiteSpace(errorContent) 
                ? $"User deletion failed with status {response.StatusCode}" 
                : errorContent;
            
            return RequestResult.CreateError(errorMessage, response.StatusCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting user: {ex.Message}");
            return RequestResult.CreateError($"Network error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<UserInfo?> GetCurrentUserInfoAsync()
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions key not configured");

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/GetCurrentUser?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, _jsonOptions);
            
            return apiResponse?.User;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting current user info: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<RequestResult> UpdatePreferredEmailAsync(string? preferredEmail)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions key not configured");

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/api/UpdatePreferredEmail?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            // Add the preferred email as request body
            var requestData = new { PreferredEmail = preferredEmail };
            var requestJson = JsonSerializer.Serialize(requestData, _jsonOptions);
            requestMessage.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (response.IsSuccessStatusCode)
            {
                return new RequestResult { Success = true };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new RequestResult 
            { 
                Success = false, 
                ErrorMessage = $"API error: {response.StatusCode} - {errorContent}" 
            };
        }
        catch (Exception ex)
        {
            return new RequestResult 
            { 
                Success = false, 
                ErrorMessage = $"Error updating preferred email: {ex.Message}" 
            };
        }
    }

    /// <inheritdoc />
    public async Task<RequestResult> UpdateEmailNotificationSettingsAsync(bool emailNotificationsEnabled, SharedDump.Models.Account.EmailReportFrequency emailFrequency)
    {
        try
        {
            var baseUrl = _configuration["FeedbackApi:BaseUrl"] 
                ?? throw new InvalidOperationException("API base URL not configured");
            var code = _configuration["FeedbackApi:FunctionsKey"]
                ?? throw new InvalidOperationException("Functions key not configured");

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/api/UpdateEmailNotificationSettings?code={Uri.EscapeDataString(code)}");

            // Add authentication headers
            await _authHeaderService.AddAuthenticationHeadersAsync(requestMessage);

            // Add the email notification settings as request body
            var requestData = new { EmailNotificationsEnabled = emailNotificationsEnabled, EmailFrequency = emailFrequency };
            var requestJson = JsonSerializer.Serialize(requestData, _jsonOptions);
            requestMessage.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (response.IsSuccessStatusCode)
            {
                return new RequestResult { Success = true };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new RequestResult 
            { 
                Success = false, 
                ErrorMessage = $"API error: {response.StatusCode} - {errorContent}" 
            };
        }
        catch (Exception ex)
        {
            return new RequestResult 
            { 
                Success = false, 
                ErrorMessage = $"Error updating email notification settings: {ex.Message}" 
            };
        }
    }

    /// <summary>
    /// API response wrapper for user information
    /// </summary>
    private class ApiResponse
    {
        public bool Success { get; set; }
        public UserInfo? User { get; set; }
    }
}
