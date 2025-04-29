using System.Text.Json.Serialization;
using SharedDump.Models.GitHub;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<GithubDiscussionModel>))]
[JsonSerializable(typeof(List<GithubIssueModel>))]
[JsonSerializable(typeof(GithubCommentModel[]))]
public partial class GithubJsonContext : JsonSerializerContext { }