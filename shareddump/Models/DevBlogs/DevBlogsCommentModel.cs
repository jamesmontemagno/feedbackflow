#nullable enable
using System;
using System.Collections.Generic;

namespace SharedDump.Models.DevBlogs;

/// <summary>
/// Represents a comment on a DevBlogs article, including possible replies.
/// </summary>
public class DevBlogsCommentModel
{
    /// <summary>
    /// The comment's unique identifier (from RSS guid or link).
    /// </summary>
    public string? Id { get; set; }
    /// <summary>
    /// The display name of the comment author.
    /// </summary>
    public string? Author { get; set; }
    /// <summary>
    /// The comment text (HTML allowed).
    /// </summary>
    public string? BodyHtml { get; set; }
    /// <summary>
    /// The date/time the comment was posted (UTC).
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; set; }
    /// <summary>
    /// The parent comment's Id, if this is a reply.
    /// </summary>
    public string? ParentId { get; set; }
    /// <summary>
    /// Replies to this comment.
    /// </summary>
    public List<DevBlogsCommentModel> Replies { get; set; } = new();
}
