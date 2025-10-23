using System.Text.Json.Serialization;

namespace SharedDump.Models.BlueSkyFeedback.ApiModels;

/// <summary>
/// Root response from BlueSky search API
/// </summary>
public class BlueSkySearchResponse
{
    [JsonPropertyName("posts")]
    public List<BlueSkySearchPost>? Posts { get; set; }
    
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// A single post from search results
/// </summary>
public class BlueSkySearchPost
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
    
    [JsonPropertyName("cid")]
    public string Cid { get; set; } = string.Empty;
    
    [JsonPropertyName("author")]
    public BlueSkySearchAuthor? Author { get; set; }
    
    [JsonPropertyName("record")]
    public BlueSkySearchRecord? Record { get; set; }
    
    [JsonPropertyName("replyCount")]
    public int ReplyCount { get; set; }
    
    [JsonPropertyName("repostCount")]
    public int RepostCount { get; set; }
    
    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }
    
    [JsonPropertyName("indexedAt")]
    public string IndexedAt { get; set; } = string.Empty;
}

/// <summary>
/// Author information from search
/// </summary>
public class BlueSkySearchAuthor
{
    [JsonPropertyName("did")]
    public string Did { get; set; } = string.Empty;
    
    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;
    
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// Post record with content
/// </summary>
public class BlueSkySearchRecord
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
