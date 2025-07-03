using SharedDump.Models.Authentication;

namespace SharedDump.Services.Authentication;

/// <summary>
/// Service for managing authenticated users in Azure Table Storage
/// </summary>
public interface IAuthUserTableService
{
    /// <summary>
    /// Get user by authentication provider and provider user ID
    /// </summary>
    /// <param name="provider">Authentication provider name</param>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <returns>The user entity or null if not found</returns>
    Task<AuthUserEntity?> GetUserByProviderAsync(string provider, string providerUserId);

    /// <summary>
    /// Get user by email address using the email index
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <returns>The user entity or null if not found</returns>
    Task<AuthUserEntity?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Get user by internal user ID
    /// </summary>
    /// <param name="userId">Internal unique user ID</param>
    /// <returns>The user entity or null if not found</returns>
    Task<AuthUserEntity?> GetUserByIdAsync(string userId);

    /// <summary>
    /// Create or update a user in the table
    /// </summary>
    /// <param name="user">The user entity to create or update</param>
    /// <returns>The updated user entity</returns>
    Task<AuthUserEntity> CreateOrUpdateUserAsync(AuthUserEntity user);

    /// <summary>
    /// Deactivate a user by setting IsActive to false
    /// </summary>
    /// <param name="userId">Internal unique user ID</param>
    /// <returns>True if successful, false if user not found</returns>
    Task<bool> DeactivateUserAsync(string userId);

    /// <summary>
    /// Get all active users (for admin purposes)
    /// </summary>
    /// <returns>Collection of active users</returns>
    Task<IEnumerable<AuthUserEntity>> GetActiveUsersAsync();

    /// <summary>
    /// Update the email index for a user
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="userId">Internal user ID</param>
    /// <param name="provider">Authentication provider</param>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <returns>Task representing the operation</returns>
    Task UpdateEmailIndexAsync(string email, string userId, string provider, string providerUserId);

    /// <summary>
    /// Update the user's preferred email address
    /// </summary>
    /// <param name="provider">Authentication provider name</param>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <param name="preferredEmail">New preferred email address (can be null to clear)</param>
    /// <returns>True if successful, false if user not found</returns>
    Task<bool> UpdatePreferredEmailAsync(string provider, string providerUserId, string? preferredEmail);
}