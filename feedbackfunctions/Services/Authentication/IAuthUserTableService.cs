using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedDump.Models.Authentication;

namespace FeedbackFunctions.Services.Authentication;

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
}
