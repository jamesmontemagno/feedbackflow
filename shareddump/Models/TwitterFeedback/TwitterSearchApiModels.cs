using System.Text.Json.Serialization;

namespace SharedDump.Models.TwitterFeedback;

/// <summary>
/// Root response from Twitter search API
/// </summary>
public class TwitterSearchResponse
{
    [JsonPropertyName("data")]
    public List<TwitterSearchTweet>? Data { get; set; }
    
    [JsonPropertyName("includes")]
    public TwitterSearchIncludes? Includes { get; set; }
    
    [JsonPropertyName("meta")]
    public TwitterSearchMeta? Meta { get; set; }
}

/// <summary>
/// A single tweet from search results
/// </summary>
public class TwitterSearchTweet
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("public_metrics")]
    public TwitterPublicMetrics? PublicMetrics { get; set; }
}

/// <summary>
/// Public metrics for a tweet
/// </summary>
public class TwitterPublicMetrics
{
    [JsonPropertyName("retweet_count")]
    public int RetweetCount { get; set; }
    
    [JsonPropertyName("reply_count")]
    public int ReplyCount { get; set; }
    
    [JsonPropertyName("like_count")]
    public int LikeCount { get; set; }
    
    [JsonPropertyName("quote_count")]
    public int QuoteCount { get; set; }
}

/// <summary>
/// Includes section with expanded user data
/// </summary>
public class TwitterSearchIncludes
{
    [JsonPropertyName("users")]
    public List<TwitterSearchUser>? Users { get; set; }
}

/// <summary>
/// User information
/// </summary>
public class TwitterSearchUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Metadata about the search results
/// </summary>
public class TwitterSearchMeta
{
    [JsonPropertyName("result_count")]
    public int ResultCount { get; set; }
    
    [JsonPropertyName("newest_id")]
    public string? NewestId { get; set; }
    
    [JsonPropertyName("oldest_id")]
    public string? OldestId { get; set; }
    
    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }
}
