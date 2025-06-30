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

    [JsonPropertyName("num_comments")]
    public int NumComments { get; set; }

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
    public DateTimeOffset CreatedUtc { get; set; }
    public List<RedditCommentModel>? Replies { get; set; } = [];
}

// Generic Reddit API models for unified interface
public class RedditListing<T>
{
    public RedditListingData<T> Data { get; set; } = new();
}

public class RedditListingData<T>
{
    public List<RedditThing<T>> Children { get; set; } = new();
    public string? After { get; set; }
    public string? Before { get; set; }
}

public class RedditThing<T>
{
    public string Kind { get; set; } = "";
    public T Data { get; set; } = default!;
}

public class RedditSubmission
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelfText { get; set; } = "";
    public string Author { get; set; } = "";
    public string Subreddit { get; set; } = "";
    public string SubredditNamePrefixed { get; set; } = "";
    public long Created { get; set; }
    public long CreatedUtc { get; set; }
    public int Score { get; set; }
    public float UpvoteRatio { get; set; }
    public int NumComments { get; set; }
    public string Url { get; set; } = "";
    public string Permalink { get; set; } = "";
    public bool IsSelf { get; set; }
    public bool Over18 { get; set; }
    public string PostHint { get; set; } = "";
}

public class RedditComment
{
    public string Id { get; set; } = "";
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public long Created { get; set; }
    public long CreatedUtc { get; set; }
    public int Score { get; set; }
    public string ParentId { get; set; } = "";
    public string LinkId { get; set; } = "";
    public string Subreddit { get; set; } = "";
    public string SubredditNamePrefixed { get; set; } = "";
    public string Permalink { get; set; } = "";
}

public class RedditSubredditInfo
{
    public string DisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string PublicDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public long? CreatedUtc { get; set; }
    public int Subscribers { get; set; }
    public int AccountsActive { get; set; }
    public bool Over18 { get; set; }
    public string SubredditType { get; set; } = "";
}

public class RedditSubredditData
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";
    
    public string Title { get; set; } = "";
    
    [JsonPropertyName("public_description")]  
    public string PublicDescription { get; set; } = "";
    
    public string Description { get; set; } = "";
    
    [JsonPropertyName("created_utc")]
    public long? CreatedUtc { get; set; }
    
    public int Subscribers { get; set; }
    
    [JsonPropertyName("accounts_active")]
    public int AccountsActive { get; set; }
    
    public bool Over18 { get; set; }
    
    [JsonPropertyName("subreddit_type")]
    public string SubredditType { get; set; } = "";
}

public class RedditSubredditResponse
{
    public string Kind { get; set; } = "";
    public RedditSubredditData Data { get; set; } = new();
}