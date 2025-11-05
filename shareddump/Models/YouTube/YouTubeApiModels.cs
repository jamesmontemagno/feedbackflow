namespace SharedDump.Models.YouTube;

public class YouTubeSearchResponse
{
    public string? NextPageToken { get; set; }
    public required YouTubeSearchResponsePageInfo PageInfo { get; set; }
    public required List<YouTubeSearchItem> Items { get; set; }
}

public class YouTubeSearchResponsePageInfo
{
    public required int TotalResults { get; set; }
    public required int ResultsPerPage { get; set; }
}

public class YouTubeSearchItem
{
    public required YouTubeSearchId Id { get; set; }
    public required YouTubeSearchSnippet Snippet { get; set; }
}

public class YouTubeSearchId
{
    public required string Kind { get; set; }
    public required string VideoId { get; set; }
}

public class YouTubeSearchSnippet
{
    public required string PublishedAt { get; set; }
    public required string ChannelId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ChannelTitle { get; set; }
}

public class YouTubeVideoStatisticsResponse
{
    public required List<YouTubeVideoStatisticsItem> Items { get; set; }
}

public class YouTubeVideoStatisticsItem
{
    public required string Id { get; set; }
    public required YouTubeStatistics Statistics { get; set; }
}

public class YouTubeStatistics
{
    public string? ViewCount { get; set; }
    public string? LikeCount { get; set; }
    public string? CommentCount { get; set; }
}

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

public class YouTubeCaptionListResponse
{
    public required List<YouTubeCaptionTrack> Items { get; set; }
}

public class YouTubeCaptionTrack
{
    public required string Id { get; set; }
    public required YouTubeCaptionSnippet Snippet { get; set; }
}

public class YouTubeCaptionSnippet
{
    public required string VideoId { get; set; }
    public required string Language { get; set; }
    public required string Name { get; set; }
    public string? TrackKind { get; set; }
}