using System.Text.Json.Serialization;

public class YouTubeVideoResponse
{
    public string? NextPageToken { get; set; } // Token for pagination
    public required List<YouTubeVideoItem> Items { get; set; }
}

public class YouTubeVideoItem
{
    public required YouTubeVideoSnippet Snippet { get; set; }
    public YouTubeVideoContentDetails? ContentDetails { get; set; }
}

public class YouTubeVideoSnippet
{
    public required string Title { get; set; }
    public DateTime PublishedAt { get; set; }
    public YouTubeResourceId? ResourceId { get; set; }
}

public class YouTubeResourceId
{
    public required string Kind { get; set; } // e.g., "youtube#video"
    public required string VideoId { get; set; }
}

public class YouTubeVideoContentDetails
{
    public required string VideoId { get; set; } // Video ID
}

public class YouTubeCommentResponse
{
    public required List<YouTubeCommentItem> Items { get; set; }
    public string? NextPageToken { get; set; }
}

public class YouTubeCommentItem
{
    public required string Id { get; set; }
    public required YouTubeCommentSnippet Snippet { get; set; }
    public YouTubeCommentReplies Replies { get; set; } = new();
}

public class YouTubeCommentSnippet
{
    public required YouTubeComment TopLevelComment { get; set; }
}

public class YouTubeCommentReplies
{
    public List<YouTubeComment> Comments { get; set; } = new();
}

public class YouTubeComment
{
    public required string Id { get; set; }
    public required YouTubeCommentDetails Snippet { get; set; }
}

public class YouTubeCommentDetails
{
    public required string AuthorDisplayName { get; set; }
    public required string TextDisplay { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? ParentId { get; set; } // This field is used for replies
}


// Output models
public class YouTubeOutputVideo
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public DateTime UploadDate { get; set; }
    public List<YouTubeOutputComment> Comments { get; set; } = new();
}

public class YouTubeOutputComment
{
    public string? ParentId { get; init; }
    public required string Id { get; set; }
    public required string? Author { get; init; }
    public required string? Text { get; init; }
    public required DateTime PublishedAt { get; init; }
}

public class YouTubeInputFile
{
    public string[]? Videos { get; set; }

    public string[]? Playlists { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]

// Input models
[JsonSerializable(typeof(YouTubeInputFile))]

// Output models
[JsonSerializable(typeof(YouTubeOutputComment))]
[JsonSerializable(typeof(YouTubeOutputVideo[]))]

[JsonSerializable(typeof(YouTubeVideoResponse))]
[JsonSerializable(typeof(YouTubeVideoItem))]
[JsonSerializable(typeof(YouTubeVideoSnippet))]
[JsonSerializable(typeof(YouTubeResourceId))]
[JsonSerializable(typeof(YouTubeVideoContentDetails))]
[JsonSerializable(typeof(YouTubeCommentResponse))]
[JsonSerializable(typeof(YouTubeCommentItem))]
[JsonSerializable(typeof(YouTubeCommentSnippet))]
[JsonSerializable(typeof(YouTubeCommentReplies))]
[JsonSerializable(typeof(YouTubeComment))]
[JsonSerializable(typeof(YouTubeCommentDetails))]
[JsonSerializable(typeof(YouTubeOutputVideo))]
[JsonSerializable(typeof(YouTubeOutputComment))]
public partial class YouTubeJsonContext : JsonSerializerContext
{
}


