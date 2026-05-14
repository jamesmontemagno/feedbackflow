using System.Text.Json.Serialization;
using SharedDump.Models.Mastodon.ApiModels;

namespace SharedDump.Models.Mastodon;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MastodonStatus))]
[JsonSerializable(typeof(MastodonAccount))]
[JsonSerializable(typeof(MastodonContext))]
public partial class MastodonFeedbackJsonContext : JsonSerializerContext
{
}
