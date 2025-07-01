using System.Text.Json.Serialization;

namespace SharedDump.Models.Authentication;

/// <summary>
/// Azure Easy Auth Client Principal model
/// </summary>
public class ClientPrincipal
{
	/// <summary>
	/// The identity provider (e.g., "microsoftaccount", "google", "github")
	/// </summary>
	[JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; } = string.Empty;

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