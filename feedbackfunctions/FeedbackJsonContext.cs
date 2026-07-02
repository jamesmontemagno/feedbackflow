using System.Text.Json.Serialization;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.ContentSearch;
using SharedDump.Models.Reports;
using FeedbackFunctions.FeedbackAnalysis;

namespace FeedbackFunctions;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GithubCommentModel[]))]
[JsonSerializable(typeof(GithubDiscussionModel[]))]
[JsonSerializable(typeof(GithubIssueModel[]))]
[JsonSerializable(typeof(HackerNewsItem[]))]
[JsonSerializable(typeof(YouTubeOutputVideo[]))]
[JsonSerializable(typeof(RedditThreadModel[]))]
[JsonSerializable(typeof(RedditReportRawData))]
[JsonSerializable(typeof(AnalyzeCommentsRequest))]
[JsonSerializable(typeof(SaveAnalysisRequest))]
[JsonSerializable(typeof(UpdateVisibilityRequest))]
[JsonSerializable(typeof(RegisterUserRequest))]
[JsonSerializable(typeof(OmniSearchRequest))]
[JsonSerializable(typeof(OmniSearchResponse))]
[JsonSerializable(typeof(OmniSearchResult))]
[JsonSerializable(typeof(List<OmniSearchResult>))]
[JsonSerializable(typeof(DeveloperReportRequest))]
[JsonSerializable(typeof(DeveloperReportResponse))]
[JsonSerializable(typeof(DeveloperReportSourceResult))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<DeveloperReportSourceResult>))]
public partial class FeedbackJsonContext : JsonSerializerContext { }