using System.Text.Json.Serialization;

namespace FeedbackFunctions;

/// <summary>
/// Request model for user registration containing optional preferred email
/// </summary>
public class RegisterUserRequest
{
    /// <summary>
    /// Optional preferred email address for the user
    /// </summary>
    [JsonPropertyName("preferredEmail")]
    public string? PreferredEmail { get; set; }
}
