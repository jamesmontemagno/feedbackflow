using System.Text.Json.Serialization;

namespace SharedDump.Models.BlueSkyFeedback;

/// <summary>
/// Represents a request to fetch BlueSky feedback for a given post or thread.
/// </summary>
public class BlueSkyFeedbackRequest
{
    /// <summary>
    /// The URL or ID of the post to fetch.
    /// </summary>
    public string PostUrlOrId { get; set; } = null!;
}

/// <summary>
/// Represents a response containing BlueSky feedback items.
/// </summary>
public class BlueSkyFeedbackResponse
{
    /// <summary>
    /// The root feedback items (posts/replies).
    /// </summary>
    public List<BlueSkyFeedbackItem> Items { get; set; } = new();
    
    /// <summary>
    /// Indicates if the results may be incomplete due to rate limits or errors
    /// </summary>
    public bool MayBeIncomplete { get; set; }
    
    /// <summary>
    /// Message providing details about rate limits or other potential issues
    /// </summary>
    public string? RateLimitInfo { get; set; }
    
    /// <summary>
    /// The number of unique posts processed during this request
    /// </summary>
    public int ProcessedPostCount { get; set; }
}
