using System.Text.Json.Serialization;

namespace SharedDump.Models.Reddit;

public class RedditTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}

public class RedditListing
{
    public required string Kind { get; set; }
    public required RedditListingData Data { get; set; }
}

public class RedditListingData
{
    public required RedditThingData[] Children { get; set; }
}

public class RedditThingData
{
    public required string Kind { get; set; }
    public required RedditCommentData Data { get; set; }
}

public class RedditCommentData
{
    public required string Id { get; set; }
    public string? ParentId { get; set; }
    public required string Author { get; set; }
    public string? Body { get; set; }
    public required string Permalink { get; set; }

    //[JsonPropertyName("created_utc")]
    //public long CreatedUtc { get; set; }
    public required int Score { get; set; }
    public string? Title { get; set; }
    public string? Selftext { get; set; }
    public object? Replies { get; set; }
    [JsonIgnore]
    public RedditListing? RepliesDisplay
    {
        get => Replies as RedditListing;
        set => Replies = value;
    }
}