#nullable enable
namespace SharedDump.Models.DevBlogs;

/// <summary>
/// Represents a DevBlogs article and its associated comments.
/// </summary>
public class DevBlogsArticleModel
{
    /// <summary>
    /// The article title.
    /// </summary>
    public string? Title { get; set; }
    /// <summary>
    /// The article URL.
    /// </summary>
    public string? Url { get; set; }
    /// <summary>
    /// The list of top-level comments (with nested replies).
    /// </summary>
    public List<DevBlogsCommentModel> Comments { get; set; } = new();
}

/// <summary>
/// Represents a DevBlogs article for unified interface.
/// </summary>
public class DevBlogsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTimeOffset PublishDate { get; set; }
    public string[] Authors { get; set; } = Array.Empty<string>();
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string Guid { get; set; } = string.Empty;
}
