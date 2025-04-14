using System.Text.Json.Serialization;
using SharedDump.Models.YouTube;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(YouTubeOutputVideo[]))]
[JsonSerializable(typeof(YouTubeOutputComment))]
public partial class YouTubeJsonContext : JsonSerializerContext { }