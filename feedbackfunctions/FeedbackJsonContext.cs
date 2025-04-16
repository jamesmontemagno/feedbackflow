using System.Text.Json.Serialization;

namespace FeedbackFunctions;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(AnalyzeCommentsRequest))]
[JsonSerializable(typeof(AnalyzeCommentsBYOKRequest))]
public partial class FeedbackJsonContext : JsonSerializerContext { }