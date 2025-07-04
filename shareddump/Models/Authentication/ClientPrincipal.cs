using System.Text.Json.Serialization;

namespace SharedDump.Models.Authentication;

/// <summary>
/// Azure Easy Auth Client Principal model
/// </summary>
public class ClientPrincipal
{
	/// <summary>
	/// The identity provider (e.g., "aad", "google", "github")
	/// </summary>
	[JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; } = string.Empty;

    /// <summary>
    /// Alternative property name for identity provider used by Azure Easy Auth
    /// </summary>
    [JsonPropertyName("auth_typ")]
    public string? AuthType { get; set; }

    /// <summary>
    /// The user ID from the provider
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The user details (usually email)
    /// </summary>
    [JsonPropertyName("userDetails")]
    public string UserDetails { get; set; } = string.Empty;

    /// <summary>
    /// The user roles
    /// </summary>
    [JsonPropertyName("userRoles")]
    public string[]? UserRoles { get; set; }

    /// <summary>
    /// The user claims
    /// </summary>
    [JsonPropertyName("claims")]
    public ClientPrincipalClaim[]? Claims { get; set; }

    /// <summary>
    /// Name type used by Azure Easy Auth
    /// </summary>
    [JsonPropertyName("name_typ")]
    public string? NameType { get; set; }

    /// <summary>
    /// Role type used by Azure Easy Auth
    /// </summary>
    [JsonPropertyName("role_typ")]
    public string? RoleType { get; set; }

    /// <summary>
    /// Gets the effective identity provider, prioritizing AuthType over IdentityProvider
    /// </summary>
    public string GetEffectiveIdentityProvider()
    {
        return !string.IsNullOrEmpty(AuthType) ? AuthType : IdentityProvider;
    }

    /// <summary>
    /// Gets the effective user ID from claims if not set directly
    /// </summary>
    public string GetEffectiveUserId()
    {
        if (!string.IsNullOrEmpty(UserId))
            return UserId;

        // Try to get user ID from claims
        return Claims?.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Gets the effective user details (email) from claims if not set directly
    /// </summary>
    public string GetEffectiveUserDetails()
    {
        if (!string.IsNullOrEmpty(UserDetails))
            return UserDetails;

        // Try to get email from claims
        return Claims?.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value ?? string.Empty;
    }
}

/// <summary>
/// Represents a claim from Azure Easy Auth
/// </summary>
public class ClientPrincipalClaim
{
    /// <summary>
    /// The claim type
    /// </summary>
    [JsonPropertyName("typ")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The claim value
    /// </summary>
    [JsonPropertyName("val")]
    public string Value { get; set; } = string.Empty;
}