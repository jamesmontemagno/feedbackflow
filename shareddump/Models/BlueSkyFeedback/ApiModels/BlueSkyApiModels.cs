using System.Text.Json.Serialization;

namespace SharedDump.Models.BlueSkyFeedback.ApiModels;

/// <summary>
/// Root object for BlueSky API thread response
/// </summary>
public class BlueSkyThreadResponse
{
    /// <summary>
    /// The thread view containing the post and its replies
    /// </summary>
    [JsonPropertyName("thread")]
    public BlueSkyThreadView? Thread { get; set; }
}

/// <summary>
/// Represents a thread view from the BlueSky API
/// </summary>
public class BlueSkyThreadView
{
    /// <summary>
    /// The type of the thread view
    /// </summary>
    [JsonPropertyName("$type")]
    public string? Type { get; set; }
    
    /// <summary>
    /// The main post in the thread
    /// </summary>
    [JsonPropertyName("post")]
    public BlueSkyPost? Post { get; set; }
    
    /// <summary>
    /// Replies to the main post
    /// </summary>
    [JsonPropertyName("replies")]
    public List<BlueSkyThreadView>? Replies { get; set; }
}

/// <summary>
/// Represents a BlueSky post from the API
/// </summary>
public class BlueSkyPost
{
    /// <summary>
    /// The URI of the post, formatted as at://did:plc:abc123/app.bsky.feed.post/xyz
    /// </summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
    
    /// <summary>
    /// The content identifier
    /// </summary>
    [JsonPropertyName("cid")]
    public string? Cid { get; set; }
    
    /// <summary>
    /// The author of the post
    /// </summary>
    [JsonPropertyName("author")]
    public BlueSkyAuthor? Author { get; set; }
    
    /// <summary>
    /// The post record containing the actual content
    /// </summary>
    [JsonPropertyName("record")]
    public BlueSkyRecord? Record { get; set; }
    
    /// <summary>
    /// Number of replies to this post
    /// </summary>
    [JsonPropertyName("replyCount")]
    public int ReplyCount { get; set; }
    
    /// <summary>
    /// Number of reposts
    /// </summary>
    [JsonPropertyName("repostCount")]
    public int RepostCount { get; set; }
    
    /// <summary>
    /// Number of likes
    /// </summary>
    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }
    
    /// <summary>
    /// UTC timestamp when the post was indexed
    /// </summary>
    [JsonPropertyName("indexedAt")]
    public DateTime IndexedAt { get; set; }
}

/// <summary>
/// Represents the author of a BlueSky post
/// </summary>
public class BlueSkyAuthor
{
    /// <summary>
    /// The decentralized identifier of the author
    /// </summary>
    [JsonPropertyName("did")]
    public string? Did { get; set; }
    
    /// <summary>
    /// The handle of the author (username)
    /// </summary>
    [JsonPropertyName("handle")]
    public string? Handle { get; set; }
    
    /// <summary>
    /// The display name of the author
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// URL to the author's avatar
    /// </summary>
    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}

/// <summary>
/// Represents a BlueSky post record containing the content
/// </summary>
public class BlueSkyRecord
{
    /// <summary>
    /// The type of the record
    /// </summary>
    [JsonPropertyName("$type")]
    public string? Type { get; set; }
    
    /// <summary>
    /// When the post was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The text content of the post
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    /// <summary>
    /// Reply information if this post is a reply
    /// </summary>
    [JsonPropertyName("reply")]
    public BlueSkyReply? Reply { get; set; }
    
    /// <summary>
    /// The languages of the post
    /// </summary>
    [JsonPropertyName("langs")]
    public List<string>? Langs { get; set; }
}

/// <summary>
/// Represents reply information within a BlueSky post
/// </summary>
public class BlueSkyReply
{
    /// <summary>
    /// The parent post information
    /// </summary>
    [JsonPropertyName("parent")]
    public BlueSkyReplyRef? Parent { get; set; }
    
    /// <summary>
    /// The root post information
    /// </summary>
    [JsonPropertyName("root")]
    public BlueSkyReplyRef? Root { get; set; }
}

/// <summary>
/// Represents a reference to a post in a reply context
/// </summary>
public class BlueSkyReplyRef
{
    /// <summary>
    /// The content identifier
    /// </summary>
    [JsonPropertyName("cid")]
    public string? Cid { get; set; }
    
    /// <summary>
    /// The URI of the referenced post
    /// </summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

/// <summary>
/// Authentication request model for BlueSky API
/// </summary>
public class BlueSkyAuthRequest
{
    /// <summary>
    /// Username or identifier for login
    /// </summary>
    [JsonPropertyName("identifier")]
    public required string Identifier { get; set; }
    
    /// <summary>
    /// App password for authentication
    /// </summary>
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}

/// <summary>
/// Authentication response model from BlueSky API
/// </summary>
public class BlueSkyAuthResponse
{
    /// <summary>
    /// Access JWT token
    /// </summary>
    [JsonPropertyName("accessJwt")]
    public string? AccessJwt { get; set; }
    
    /// <summary>
    /// Refresh JWT token
    /// </summary>
    [JsonPropertyName("refreshJwt")]
    public string? RefreshJwt { get; set; }
    
    /// <summary>
    /// User DID
    /// </summary>
    [JsonPropertyName("did")]
    public string? Did { get; set; }
}
