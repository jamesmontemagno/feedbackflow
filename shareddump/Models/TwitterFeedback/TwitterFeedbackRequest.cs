namespace SharedDump.Models.TwitterFeedback;

/// <summary>
/// Represents a request to fetch Twitter/X feedback for a given tweet or thread.
/// </summary>
public class TwitterFeedbackRequest
{
    /// <summary>
    /// The URL or ID of the tweet/thread to fetch feedback for.
    /// </summary>
    public string TweetUrlOrId { get; set; } = null!;
}

/// <summary>
/// Represents a response containing Twitter/X feedback items.
/// </summary>
public class TwitterFeedbackResponse
{
    /// <summary>
    /// The root feedback items (tweets/replies).
    /// </summary>
    public List<TwitterFeedbackItem> Items { get; set; } = new();
    
    /// <summary>
    /// Indicates if the results may be incomplete due to rate limits or errors
    /// </summary>
    public bool MayBeIncomplete { get; set; }
    
    /// <summary>
    /// Message providing details about rate limits or other potential issues
    /// </summary>
    public string? RateLimitInfo { get; set; }
    
    /// <summary>
    /// The number of unique tweets processed during this request
    /// </summary>
    public int ProcessedTweetCount { get; set; }
}
