namespace SharedDump.Models.YouTube;

public class YouTubeOutputVideo
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public DateTime UploadDate { get; set; }
    public List<YouTubeOutputComment> Comments { get; set; } = [];
    public YouTubeTranscript? Transcript { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
    public string ChannelId { get; set; } = "";
    public string ChannelTitle { get; set; } = "";
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
}

public class YouTubeOutputComment
{
    public string? ParentId { get; init; }
    public required string Id { get; set; }
    public required string? Author { get; init; }
    public required string? Text { get; init; }
    public required DateTime PublishedAt { get; init; }
}

public class YouTubeCommentDetails
{
    public required string AuthorDisplayName { get; set; }
    public required string TextDisplay { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? ParentId { get; set; }
}

public class YouTubeTranscript
{
    public required string VideoId { get; set; }
    public required string Language { get; set; }
    public required List<YouTubeTranscriptSegment> Segments { get; set; }
    public string FullText => string.Join(" ", Segments.Select(s => s.Text));
}

public class YouTubeTranscriptSegment
{
    public required string Text { get; set; }
    public required double Start { get; set; }
    public required double Duration { get; set; }
}

public enum YouTubeContentType
{
    Comments,
    Transcript,
    Both
}