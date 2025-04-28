using System.Text.Json.Serialization;

namespace SharedDump.Models.GitHub;

public class GithubDiscussionModel
{
    public required string Title { get; set; }
    public string? AnswerId { get; set; }
    public required string Url { get; set; }
    public required GithubCommentModel[] Comments { get; set; }
}

public class GithubIssueModel
{
    public required string Id { get; set; }
    public required string Author { get; set; }
    public required string Title { get; set; }
    public required string URL { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastUpdated { get; set; }
    public required string Body { get; set; }
    public required int Upvotes { get; set; }
    public required IEnumerable<string> Labels { get; set; }
    public required GithubCommentModel[] Comments { get; set; }
}

public class GithubCommentModel
{
    public required string Id { get; set; }
    public string? ParentId { get; set; }
    public required string Author { get; set; }
    public required string Content { get; set; }
    public required string CreatedAt { get; set; }
    public required string Url { get; set; }
    
    // Properties for code review comments
    public string? CodeContext { get; set; }
    public string? FilePath { get; set; }
    public int? LinePosition { get; set; }
}