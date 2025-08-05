using System.Text.Json.Serialization;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;

namespace FeedbackFunctions;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GithubCommentModel[]))]
[JsonSerializable(typeof(GithubDiscussionModel[]))]
[JsonSerializable(typeof(GithubIssueModel[]))]
[JsonSerializable(typeof(HackerNewsItem[]))]
[JsonSerializable(typeof(YouTubeOutputVideo[]))]
[JsonSerializable(typeof(RedditThreadModel[]))]
[JsonSerializable(typeof(AnalyzeCommentsRequest))]
[JsonSerializable(typeof(SaveAnalysisRequest))]
[JsonSerializable(typeof(UpdateVisibilityRequest))]
[JsonSerializable(typeof(RegisterUserRequest))]
public partial class FeedbackJsonContext : JsonSerializerContext { }