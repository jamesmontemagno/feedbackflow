namespace SharedDump.Models.BlueSkyFeedback;

/// <summary>
/// Represents a single BlueSky feedback item (post or reply).
/// </summary>
public class BlueSkyFeedbackItem
{
    /// <summary>
    /// The unique ID of the post.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The username of the author.
    /// </summary>
    public string Author { get; set; } = null!;

    /// <summary>
    /// The display name of the author.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// The username of the author (without @ symbol).
    /// </summary>
    public string? AuthorUsername { get; set; }

    /// <summary>
    /// The post content.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// The UTC timestamp of the post.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// The parent post ID if this is a reply, otherwise null.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// List of child replies (nested feedback).
    /// </summary>
    public List<BlueSkyFeedbackItem>? Replies { get; set; }
}
