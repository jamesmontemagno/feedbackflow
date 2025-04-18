using System.Text.Json.Serialization;

namespace SharedDump.Models.Reddit;


public class RedditThreadModel
{
    public required string Id { get; set; }
    public required string Author { get; set; }
    public required string Title { get; set; }
    public required string SelfText { get; set; }
    public required string Url { get; set; }
    public required string Subreddit { get; set; }
    //public required DateTime CreatedUtc { get; set; }
    public required int Score { get; set; }
    public required double UpvoteRatio { get; set; }
    public required int NumComments { get; set; }
    public List<RedditCommentModel>? Comments { get; set; }
}

public class RedditCommentModel
{
    public required string Id { get; set; }
    public string? ParentId { get; set; }
    public required string Author { get; set; }
    public required string Body { get; set; }
    //public required DateTime CreatedUtc { get; set; }
    public required int Score { get; set; }
    public List<RedditCommentModel>? Replies { get; set; }
}