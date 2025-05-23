using System.Text;
using SharedDump.Models.Reddit;

namespace SharedDump.Utils;

/// <summary>
/// Provides utility methods for processing comments across different platforms.
/// </summary>
/// <remarks>
/// This class centralizes comment-processing functionality to ensure consistent
/// handling and formatting of comments from various sources.
/// </remarks>
public static class CommentProcessingUtils
{
    /// <summary>
    /// Flattens a hierarchical structure of comments into a single list.
    /// </summary>
    /// <param name="comments">The hierarchical list of comments.</param>
    /// <returns>A flattened list containing all comments.</returns>
    /// <example>
    /// <code>
    /// var flattenedComments = CommentProcessingUtils.FlattenComments(threadComments);
    /// </code>
    /// </example>
    public static List<RedditCommentModel> FlattenComments(List<RedditCommentModel> comments)
    {
        var flattened = new List<RedditCommentModel>();
        foreach (var comment in comments)
        {
            flattened.Add(comment);
            if (comment.Replies?.Any() == true)
            {
                flattened.AddRange(FlattenComments(comment.Replies));
            }
        }
        return flattened;
    }

    /// <summary>
    /// Formats comments into a readable text format for analysis.
    /// </summary>
    /// <param name="comments">The list of comments to format.</param>
    /// <returns>A string representation of the comments.</returns>
    /// <example>
    /// <code>
    /// var commentText = CommentProcessingUtils.FormatCommentsForAnalysis(flattenedComments);
    /// </code>
    /// </example>
    public static string FormatCommentsForAnalysis(IEnumerable<RedditCommentModel> comments)
    {
        return string.Join("\n", comments.Select(c => $"Comment by {c.Author}: {c.Body}"));
    }

    /// <summary>
    /// Combines thread information and comments into a formatted text for analysis.
    /// </summary>
    /// <param name="title">The thread title.</param>
    /// <param name="content">The thread content/body.</param>
    /// <param name="comments">The list of comments.</param>
    /// <returns>A formatted string combining thread info and comments.</returns>
    /// <example>
    /// <code>
    /// var analysisText = CommentProcessingUtils.CombineThreadWithComments(
    ///     thread.Title,
    ///     thread.SelfText,
    ///     flattenedComments);
    /// </code>
    /// </example>
    public static string CombineThreadWithComments(
        string title, 
        string content, 
        IEnumerable<RedditCommentModel> comments)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine($"Title: {title}");
        builder.AppendLine();
        builder.AppendLine($"Content: {content}");
        builder.AppendLine();
        builder.AppendLine("Comments:");
        builder.AppendLine(FormatCommentsForAnalysis(comments));
        
        return builder.ToString();
    }
}