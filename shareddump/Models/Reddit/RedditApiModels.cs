using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedDump.Models.Reddit;

public class RedditTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}

public class RedditListing
{
    public string Kind { get; set; } = "";
    public RedditListingData Data { get; set; } = new();
}

public class RedditListingData
{
    public RedditThingData[] Children { get; set; } = Array.Empty<RedditThingData>();
}

public class RedditThingData
{
    public string Kind { get; set; } = "";
    public RedditCommentData Data { get; set; } = new();
}

public class RedditCommentData
{
    public string Id { get; set; } = "";
    public string? ParentId { get; set; }
    public string Author { get; set; } = "";
    public string? Body { get; set; }
    public string Permalink { get; set; } = "";
    public string? Title { get; set; }
    public string? Selftext { get; set; }

    [JsonPropertyName("created_utc")]
    public double CreatedUtc { get; set; }

    public int Score { get; set; }
    
    public object? Replies { get; set; }

    [JsonIgnore]
    public RedditListing? RepliesDisplay
    {
        get
        {
            try
            {
                if (Replies is JsonElement jsonElement && jsonElement.ValueKind != JsonValueKind.String)
                {
                    try
                    {
                        return jsonElement.Deserialize<RedditListing>(new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

public class RedditThreadModel
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public string SelfText { get; set; } = "";
    public required string Url { get; set; }
    public string Subreddit { get; set; } = "";
    public int Score { get; set; }
    public double UpvoteRatio { get; set; }
    public int NumComments { get; set; }
    public List<RedditCommentModel> Comments { get; set; } = [];
    public DateTimeOffset CreatedUtc { get; set; }
    public string Permalink { get; set; } = "";
}

public class RedditCommentModel
{
    public required string Id { get; set; }
    public string? ParentId { get; set; }
    public required string Author { get; set; }
    public required string Body { get; set; }
    public int Score { get; set; }
    public List<RedditCommentModel>? Replies { get; set; } = [];
}