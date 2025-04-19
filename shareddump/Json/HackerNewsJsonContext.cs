using System.Text.Json.Serialization;
using System.Text.Json;
using SharedDump.Models.HackerNews;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HackerNewsItem))]
[JsonSerializable(typeof(IAsyncEnumerable<HackerNewsItem>))]
[JsonSerializable(typeof(int[]))]
public partial class HackerNewsJsonContext : JsonSerializerContext { }