#nullable enable
using System;
using System.Collections.Generic;

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
