using System.Text.Json.Serialization;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Models.BlueSkyFeedback.ApiModels;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BlueSkyFeedbackResponse))]
[JsonSerializable(typeof(BlueSkyFeedbackItem))]
[JsonSerializable(typeof(BlueSkyThreadResponse))]
[JsonSerializable(typeof(BlueSkyAuthRequest))]
[JsonSerializable(typeof(BlueSkyAuthResponse))]
[JsonSerializable(typeof(BlueSkySearchResponse))]
[JsonSerializable(typeof(BlueSkySearchPost))]
public partial class BlueSkyFeedbackJsonContext : JsonSerializerContext
{
}
