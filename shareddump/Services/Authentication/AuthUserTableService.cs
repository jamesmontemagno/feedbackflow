using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.Authentication;

namespace SharedDump.Services.Authentication;

/// <summary>
/// Implementation of the auth user table service using Azure Table Storage
/// </summary>
public class AuthUserTableService : IAuthUserTableService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableClient _userTableClient;
    private readonly TableClient _emailIndexTableClient;
    private readonly ILogger<AuthUserTableService> _logger;
    private const string AUTH_USERS_TABLE = "AuthUsers";
    private const string USER_EMAIL_INDEX_TABLE = "UserEmailIndex";

    /// <summary>
    /// Constructor for the auth user table service
    /// </summary>
    /// <param name="configuration">Configuration for connection string</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public AuthUserTableService(IConfiguration configuration, ILogger<AuthUserTableService> logger)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                             configuration["AzureWebJobsStorage"] ??
                             throw new InvalidOperationException("No storage connection string configured");

        _tableServiceClient = new TableServiceClient(connectionString);
        _userTableClient = _tableServiceClient.GetTableClient(AUTH_USERS_TABLE);
        _emailIndexTableClient = _tableServiceClient.GetTableClient(USER_EMAIL_INDEX_TABLE);
        _logger = logger;

        // Ensure tables exist
        _ = Task.Run(async () =>
        {
            await _userTableClient.CreateIfNotExistsAsync();
            await _emailIndexTableClient.CreateIfNotExistsAsync();
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
            // First lookup in email index
            var emailKey = email?.ToLower() ?? string.Empty;
            var partitionKey = emailKey.Length > 0 ? emailKey[0].ToString().ToUpper() : "A";
            
            var indexResponse = await _emailIndexTableClient.GetEntityIfExistsAsync<UserEmailIndexEntity>(partitionKey, emailKey);
            if (!indexResponse.HasValue)
                return null;

            var indexEntity = indexResponse.Value;
            if (indexEntity != null)
                return await GetUserByProviderAsync(indexEntity.AuthProvider, indexEntity.ProviderUserId);
            
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
    public async Task UpdateEmailIndexAsync(string email, string userId, string provider, string providerUserId)
    {
        try
        {
            var indexEntity = new UserEmailIndexEntity(email, userId, provider, providerUserId);
            await _emailIndexTableClient.UpsertEntityAsync(indexEntity);
            _logger.LogDebug("Email index updated for {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email index for {Email}", email);
            throw;
        }
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