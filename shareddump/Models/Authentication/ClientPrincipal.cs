using System.Net.Mail;
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

        // Try to get email from claims using enhanced logic
        var email = GetEmailFromClaims(GetEffectiveIdentityProvider());
        return email ?? string.Empty;
    }

    /// <summary>
    /// Get email from claims using provider-specific logic
    /// </summary>
    /// <param name="provider">Identity provider</param>
    /// <returns>Email address or null if not found</returns>
    public string? GetEmailFromClaims(string provider)
    {
        if (Claims == null) return null;

        // Standard email claim types to check, in order of preference
        var emailClaimTypes = new[]
        {
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            "email",
            "emails",
            "preferred_username"
        };

        // Provider-specific email claim types
        var providerSpecificClaims = provider switch
        {
            "github" => new[] { "urn:github:email", "urn:github:primary_email" },
            "google" => new[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn" },
            "aad" => new[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "upn" },
            _ => Array.Empty<string>()
        };

        // First, try provider-specific claims
        foreach (var claimType in providerSpecificClaims)
        {
            var claim = Claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (claim?.Value != null && IsValidEmail(claim.Value))
            {
                return claim.Value;
            }
        }

        // Then, try standard email claim types
        foreach (var claimType in emailClaimTypes)
        {
            var claim = Claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (claim?.Value != null && IsValidEmail(claim.Value))
            {
                return claim.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Simple email validation
    /// </summary>
    /// <param name="email">Email to validate</param>
    /// <returns>True if email format is valid</returns>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
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