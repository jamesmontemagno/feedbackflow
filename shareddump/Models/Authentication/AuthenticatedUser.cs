namespace SharedDump.Models.Authentication;

/// <summary>
/// Represents an authenticated user with their details
/// </summary>
public class AuthenticatedUser
{
    /// <summary>
    /// Internal unique user ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's preferred email address for notifications (independent of auth provider email)
    /// </summary>
    public string? PreferredEmail { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Authentication provider (e.g., "Microsoft", "Google")
    /// </summary>
    public string AuthProvider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific user ID
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// URL to the user's profile image
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// When the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Constructor for creating authenticated user from entity
    /// </summary>
    /// <param name="userEntity">The user entity from table storage</param>
    public AuthenticatedUser(AuthUserEntity userEntity)
    {
        UserId = userEntity.UserId;
        Email = userEntity.Email;
        Name = userEntity.Name;
        AuthProvider = userEntity.AuthProvider;
        ProviderUserId = userEntity.ProviderUserId;
        ProfileImageUrl = userEntity.ProfileImageUrl;
        CreatedAt = userEntity.CreatedAt;
        LastLoginAt = userEntity.LastLoginAt;
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public AuthenticatedUser() { }
}