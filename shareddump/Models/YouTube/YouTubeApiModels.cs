using System.Text.Json.Serialization;

namespace SharedDump.Models.YouTube;

public class YouTubeVideoResponse
{
    public string? NextPageToken { get; set; }
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
    public required string Kind { get; set; }
    public required string VideoId { get; set; }
}

public class YouTubeVideoContentDetails
{
    public required string VideoId { get; set; }
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

public class YouTubeInputFile
{
    public string[]? Videos { get; set; }
    public string[]? Playlists { get; set; }
}