using System.Text.Json.Serialization;
using SharedDump.Models.GitHub;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GithubRepositoryQuery))]
[JsonSerializable(typeof(GithubIssueQuery))]
[JsonSerializable(typeof(GithubDiscussionQuery))]
[JsonSerializable(typeof(GraphqlResponse))]
[JsonSerializable(typeof(RepositoryData))]
[JsonSerializable(typeof(Repository))]
[JsonSerializable(typeof(DiscussionConnection))]
[JsonSerializable(typeof(Discussion))]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(CommentConnection))]
[JsonSerializable(typeof(Comment))]
[JsonSerializable(typeof(Author))]
[JsonSerializable(typeof(Edge<Discussion>))]
[JsonSerializable(typeof(Edge<Comment>))]
[JsonSerializable(typeof(Edge<Issue>))]
[JsonSerializable(typeof(Edge<PullRequest>))]
[JsonSerializable(typeof(PageInfo))]
[JsonSerializable(typeof(IssueConnection))]
[JsonSerializable(typeof(Issue))]
[JsonSerializable(typeof(PullRequestConnection))]
[JsonSerializable(typeof(PullRequest))]
[JsonSerializable(typeof(Reaction))]
[JsonSerializable(typeof(LabelConnection))]
[JsonSerializable(typeof(Label))]
public partial class GithubApiJsonContext : JsonSerializerContext { }