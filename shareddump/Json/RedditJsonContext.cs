using System.Text.Json.Serialization;
using SharedDump.Models.Reddit;

namespace SharedDump.Json;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RedditListing[]))]
[JsonSerializable(typeof(RedditTokenResponse))]
[JsonSerializable(typeof(RedditListing))]
[JsonSerializable(typeof(RedditListingData))]
[JsonSerializable(typeof(RedditThingData))]
[JsonSerializable(typeof(RedditCommentData))]
[JsonSerializable(typeof(RedditThreadModel))]
[JsonSerializable(typeof(RedditCommentModel[]))]
public partial class RedditJsonContext : JsonSerializerContext { }