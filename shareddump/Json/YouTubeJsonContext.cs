using System.Text.Json.Serialization;
using SharedDump.Models.YouTube;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(YouTubeOutputVideo[]))]
[JsonSerializable(typeof(List<YouTubeOutputVideo>))]
[JsonSerializable(typeof(YouTubeOutputVideo))]
[JsonSerializable(typeof(YouTubeOutputComment))]
[JsonSerializable(typeof(YouTubeInputFile))]
[JsonSerializable(typeof(YouTubeVideoResponse))]
[JsonSerializable(typeof(YouTubeVideoItem))]
[JsonSerializable(typeof(YouTubeVideoSnippet))]
[JsonSerializable(typeof(YouTubeResourceId))]
[JsonSerializable(typeof(YouTubeVideoContentDetails))]
[JsonSerializable(typeof(YouTubeCommentResponse))]
[JsonSerializable(typeof(YouTubeCommentItem))]
[JsonSerializable(typeof(YouTubeCommentSnippet))]
[JsonSerializable(typeof(YouTubeCommentReplies))]
[JsonSerializable(typeof(YouTubeComment))]
[JsonSerializable(typeof(YouTubeCommentDetails))]
[JsonSerializable(typeof(YouTubeSearchResponse))]
[JsonSerializable(typeof(YouTubeVideoStatisticsResponse))]
[JsonSerializable(typeof(YouTubeTranscript))]
[JsonSerializable(typeof(YouTubeTranscriptSegment))]
[JsonSerializable(typeof(YouTubeCaptionListResponse))]
[JsonSerializable(typeof(YouTubeCaptionTrack))]
[JsonSerializable(typeof(YouTubeCaptionSnippet))]
public partial class YouTubeJsonContext : JsonSerializerContext { }