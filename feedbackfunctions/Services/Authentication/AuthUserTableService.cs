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
                             configuration["AzureWebJobsStorage"] ??
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
    public async Task<bool> DeactivateUserAsync(string userId)
    {
        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return false;

            user.IsActive = false;
            await CreateOrUpdateUserAsync(user);
            _logger.LogInformation("User {UserId} deactivated", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AuthUserEntity>> GetActiveUsersAsync()
    {
        var activeUsers = new List<AuthUserEntity>();
        try
        {
            var query = _userTableClient.QueryAsync<AuthUserEntity>(filter: "IsActive eq true");
            await foreach (var user in query)
            {
                activeUsers.Add(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active users");
        }
        return activeUsers;
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePreferredEmailAsync(string provider, string providerUserId, string? preferredEmail)
    {
        try
        {
            // Get the existing user
            var existingUser = await GetUserByProviderAsync(provider, providerUserId);
            if (existingUser == null)
            {
                _logger.LogWarning("User not found for provider {Provider} and user ID {ProviderUserId}", provider, providerUserId);
                return false;
            }

            // Update the preferred email
            existingUser.PreferredEmail = preferredEmail;

            // Save the updated user
            await _userTableClient.UpdateEntityAsync(existingUser, existingUser.ETag);
            
            _logger.LogInformation("Updated preferred email for user {UserId} to {PreferredEmail}", 
                existingUser.UserId, preferredEmail ?? "(cleared)");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferred email for provider {Provider} and user ID {ProviderUserId}", 
                provider, providerUserId);
            return false;
        }
    }
}
