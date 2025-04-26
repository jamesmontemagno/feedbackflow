using System.Text.Json.Serialization;

namespace SharedDump.Models.TwitterFeedback;

/// <summary>
/// Represents a single Twitter/X feedback item (tweet or reply).
/// </summary>
public class TwitterFeedbackItem
{
    /// <summary>
    /// The unique ID of the tweet.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The username of the author.
    /// </summary>
    public string Author { get; set; } = null!;    /// <summary>
    /// The display name of the author.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// The username of the author (without @ symbol).
    /// </summary>
    public string? AuthorUsername { get; set; }

    /// <summary>
    /// The tweet content.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// The UTC timestamp of the tweet.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// The parent tweet ID if this is a reply, otherwise null.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// List of child replies (nested feedback).
    /// </summary>
    public List<TwitterFeedbackItem>? Replies { get; set; }
}
