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
    /// <returns>Result of the registration operation</returns>
    Task<RequestResult> RegisterCurrentUserAsync();
    
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
}

/// <summary>
/// User information returned from the backend
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
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
    public async Task<RequestResult> RegisterCurrentUserAsync()
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

    /// <summary>
    /// API response wrapper for user information
    /// </summary>
    private class ApiResponse
    {
        public bool Success { get; set; }
        public UserInfo? User { get; set; }
    }
}
