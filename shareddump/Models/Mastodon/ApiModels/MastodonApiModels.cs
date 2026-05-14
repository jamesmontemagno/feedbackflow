using System.Text.Json.Serialization;

namespace SharedDump.Models.Mastodon.ApiModels;

public class MastodonStatus
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("account")]
    public MastodonAccount Account { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("in_reply_to_id")]
    public string? InReplyToId { get; set; }

    [JsonPropertyName("replies_count")]
    public int RepliesCount { get; set; }

    [JsonPropertyName("reblog")]
    public MastodonStatus? Reblog { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class MastodonAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;
}

public class MastodonContext
{
    [JsonPropertyName("ancestors")]
    public List<MastodonStatus> Ancestors { get; set; } = new();

    [JsonPropertyName("descendants")]
    public List<MastodonStatus> Descendants { get; set; } = new();
}
