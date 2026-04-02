using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using FeedbackFunctions.Services.Storage;

using SharedDump.Models.Authentication;

namespace FeedbackFunctions.Services.Authentication;

/// <summary>
/// Implementation of the auth user table service using Azure Table Storage
/// </summary>
public class AuthUserTableService : IAuthUserTableService
{
    private readonly TableClient _userTableClient;
    private readonly ITableInitializationService _tableInitializationService;
    private readonly ILogger<AuthUserTableService> _logger;
    private const string AUTH_USERS_TABLE = "AuthUsers";

    /// <summary>
    /// Constructor for the auth user table service
    /// </summary>
    /// <param name="configuration">Configuration for connection string</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public AuthUserTableService(
        FeedbackStorageClients storageClients,
        ITableInitializationService tableInitializationService,
        ILogger<AuthUserTableService> logger)
    {
        _userTableClient = storageClients.AuthUsersTable;
        _tableInitializationService = tableInitializationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity?> GetUserByProviderAsync(string provider, string providerUserId)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            var response = await _userTableClient.GetEntityIfExistsAsync<AuthUserEntity>(provider, providerUserId);
            return response.HasValue ? response.Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by provider {Provider} and user ID {ProviderUserId}", provider, providerUserId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity?> GetUserByEmailAsync(string email)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            // Direct query across all partitions - this is more expensive but simpler
            var query = _userTableClient.QueryAsync<AuthUserEntity>(filter: $"Email eq '{email?.ToLower()}'");
            await foreach (var user in query)
            {
                return user;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email {Email}", email);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity?> GetUserByIdAsync(string userId)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            // This requires a cross-partition query, which is more expensive
            var query = _userTableClient.QueryAsync<AuthUserEntity>(filter: $"UserId eq '{userId}'");
            await foreach (var user in query)
            {
                return user;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by user ID {UserId}", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity> CreateOrUpdateUserAsync(AuthUserEntity user)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            var existing = await GetUserByProviderAsync(user.AuthProvider, user.ProviderUserId);
            if (existing is not null)
                return existing;
            
            var response = await _userTableClient.UpsertEntityAsync(user);
            _logger.LogInformation("User {UserId} created/updated successfully", user.UserId);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating user {UserId}", user.UserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity> CreateUserIfNotExistsAsync(AuthUserEntity user)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            // First try to add the entity atomically - this will fail if it already exists
            try
            {
                await _userTableClient.AddEntityAsync(user);
                _logger.LogInformation("User {UserId} created successfully (new user)", user.UserId);
                return user;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 409)
            {
                // Entity already exists, fetch and return the existing one
                _logger.LogInformation("User already exists for {Provider} provider with ID {ProviderUserId}, returning existing user", 
                    user.AuthProvider, user.ProviderUserId);
                
                var existing = await GetUserByProviderAsync(user.AuthProvider, user.ProviderUserId);
                if (existing != null)
                {
                    // Update the existing user's last login and profile image if provided
                    if (user.LastLoginAt.HasValue)
                    {
                        existing.LastLoginAt = user.LastLoginAt;
                    }
                    if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        existing.ProfileImageUrl = user.ProfileImageUrl;
                    }
                    
                    // Try to update the existing entity with new information
                    try
                    {
                        await _userTableClient.UpdateEntityAsync(existing, existing.ETag);
                        _logger.LogInformation("Updated existing user {UserId} with latest login info", existing.UserId);
                    }
                    catch (Exception updateEx)
                    {
                        // Log the error but don't fail - we can still return the existing user
                        _logger.LogWarning(updateEx, "Failed to update existing user {UserId} with latest login info, but returning existing user", existing.UserId);
                    }
                    
                    return existing;
                }
                else
                {
                    // This shouldn't happen, but handle it gracefully
                    _logger.LogError("User entity conflict but could not retrieve existing user for {Provider} provider with ID {ProviderUserId}", 
                        user.AuthProvider, user.ProviderUserId);
                    throw new InvalidOperationException($"User creation conflict but existing user not found for {user.AuthProvider}:{user.ProviderUserId}");
                }
            }
        }
        catch (Exception ex) when (!(ex is Azure.RequestFailedException rfe && rfe.Status == 409))
        {
            _logger.LogError(ex, "Error creating user {UserId}", user.UserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUserAsync(string userId)
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return false;

            await _userTableClient.DeleteEntityAsync(user.PartitionKey, user.RowKey);
            _logger.LogInformation("User {UserId} deleted permanently", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AuthUserEntity>> GetAllUsersAsync()
    {
        await _tableInitializationService.EnsureAuthTablesAsync();

        var users = new List<AuthUserEntity>();
        try
        {
            var query = _userTableClient.QueryAsync<AuthUserEntity>();
            await foreach (var user in query)
            {
                users.Add(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
        }
        return users;
    }
}
