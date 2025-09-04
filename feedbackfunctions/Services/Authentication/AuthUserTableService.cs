using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Authentication;

namespace FeedbackFunctions.Services.Authentication;

/// <summary>
/// Implementation of the auth user table service using Azure Table Storage
/// </summary>
public class AuthUserTableService : IAuthUserTableService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableClient _userTableClient;
    private readonly ILogger<AuthUserTableService> _logger;
    private const string AUTH_USERS_TABLE = "AuthUsers";

    /// <summary>
    /// Constructor for the auth user table service
    /// </summary>
    /// <param name="configuration">Configuration for connection string</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public AuthUserTableService(IConfiguration configuration, ILogger<AuthUserTableService> logger)
    {
        var connectionString = configuration["ProductionStorage"] ??
                             throw new InvalidOperationException("No storage connection string configured");

        _tableServiceClient = new TableServiceClient(connectionString);
        _userTableClient = _tableServiceClient.GetTableClient(AUTH_USERS_TABLE);
        _logger = logger;

        // Ensure tables exist
        _ = Task.Run(async () =>
        {
            await _userTableClient.CreateIfNotExistsAsync();
        });
    }

    /// <inheritdoc />
    public async Task<AuthUserEntity?> GetUserByProviderAsync(string provider, string providerUserId)
    {
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
