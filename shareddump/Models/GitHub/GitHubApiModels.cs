using System.Text.Json.Serialization;

namespace SharedDump.Models.GitHub;

// GraphQL API models
public class GraphqlResponse
{
    public required RepositoryData Data { get; set; }
}

public class RepositoryData
{
    public required Repository? Repository { get; set; }
}

public class Repository
{
    public DiscussionConnection Discussions { get; set; } = default!;
    public IssueConnection Issues { get; set; } = default!;
    public PullRequestConnection PullRequests { get; set; } = default!;
    
    // Single item properties
    public Discussion? Discussion { get; set; }
    public Issue? Issue { get; set; }
    public PullRequest? PullRequest { get; set; }
}

public class DiscussionConnection
{
    public required List<Edge<Discussion>> Edges { get; set; }
    public required PageInfo PageInfo { get; set; }
}

public class Discussion
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public Answer? Answer { get; set; }
    public required CommentConnection Comments { get; set; }
}

public class Answer
{
    public required string Id { get; set; }
}

public class CommentConnection
{
    public required List<Edge<Comment>> Edges { get; set; }
    public PageInfo? PageInfo { get; set; }
}

public class Comment
{
    public required string Id { get; set; }
    public required string Body { get; set; }
    public required string Url { get; set; }
    public required string CreatedAt { get; set; }
    public required Author Author { get; set; }
    public CommentConnection? Replies { get; set; }
}

public class Author
{
    public string? Login { get; set; }
}

public class Edge<T>
{
    public required T Node { get; set; }
}

public class PageInfo
{
    public required bool HasNextPage { get; set; }
    public string? EndCursor { get; set; }
}

public class IssueConnection
{
    public required List<Edge<Issue>> Edges { get; set; }
    public required PageInfo PageInfo { get; set; }
}

public class Issue
{
    public required string Id { get; set; }
    public required Author Author { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Url { get; set; }
    public required string CreatedAt { get; set; }
    public required string UpdatedAt { get; set; }
    public required Reaction Reactions { get; set; }
    public required LabelConnection Labels { get; set; }
    public required CommentConnection Comments { get; set; }
}

public class PullRequestConnection
{
    public required List<Edge<PullRequest>> Edges { get; set; }
    public required PageInfo PageInfo { get; set; }
}

public class PullRequest
{
    public required string Id { get; set; }
    public required Author Author { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Url { get; set; }
    public required string CreatedAt { get; set; }
    public required string UpdatedAt { get; set; }
    public required Reaction Reactions { get; set; }
    public required LabelConnection Labels { get; set; }
    public required CommentConnection Comments { get; set; }
}

public class Reaction
{
    public required int TotalCount { get; set; }
}

public class LabelConnection
{
    public required List<Label> Nodes { get; set; }
}

public class Label
{
    public required string Name { get; set; }
}

// GraphQL queries
public class GithubRepositoryQuery
{
    public required string Query { get; set; }
    public required RepositoryQueryVariables Variables { get; set; }
    public class RepositoryQueryVariables
    {
        public required string Owner { get; set; }
        public required string Name { get; set; }
    }
}

public class GithubIssueQuery
{
    public required string Query { get; set; }
    public required IssueQueryVariables Variables { get; set; }

    public class IssueQueryVariables
    {
        public required string Owner { get; set; }
        public required string Name { get; set; }
        public string? After { get; set; }
        public string[]? Labels { get; set; }
    }
}

public class GithubDiscussionQuery
{
    public required string Query { get; set; }
    public required DiscussionQueryVariables Variables { get; set; }
    public class DiscussionQueryVariables
    {
        public required string Owner { get; set; }
        public required string Name { get; set; }
        public string? After { get; set; }
    }
}