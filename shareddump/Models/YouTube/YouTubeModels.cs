namespace SharedDump.Models.YouTube;

public class YouTubeOutputVideo
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public DateTime UploadDate { get; set; }
    public List<YouTubeOutputComment> Comments { get; set; } = [];
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