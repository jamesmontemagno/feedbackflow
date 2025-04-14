using System.Text.Json.Serialization;
using SharedDump.Models.HackerNews;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HackerNewsItem))]
[JsonSerializable(typeof(IAsyncEnumerable<HackerNewsItem>))]
public partial class HackerNewsJsonContext : JsonSerializerContext { }