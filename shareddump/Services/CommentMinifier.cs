using System.Text;
using SharedDump.Models;

namespace SharedDump.Services;

/// <summary>
/// Service for converting full comment models to minified versions for efficient data transfer
/// </summary>
public static class CommentMinifier
{
    /// <summary>
    /// Converts a full CommentThread to a minified version for analysis
    /// </summary>
    public static MinifiedCommentThread MinifyThread(CommentThread thread)
    {
        return new MinifiedCommentThread
        {
            Title = thread.Title,
            Description = thread.Description,
            Author = thread.Author,
            CreatedAt = thread.CreatedAt,
            Platform = thread.SourceType,
            Comments = MinifyComments(thread.Comments)
        };
    }

    /// <summary>
    /// Converts multiple CommentThreads to minified versions
    /// </summary>
    public static List<MinifiedCommentThread> MinifyThreads(List<CommentThread> threads)
    {
        return threads.Select(MinifyThread).ToList();
    }

    /// <summary>
    /// Converts a list of full CommentData to minified versions
    /// </summary>
    public static List<MinifiedCommentData> MinifyComments(List<CommentData> comments)
    {
        return comments.Select(MinifyComment).ToList();
    }

    /// <summary>
    /// Converts a single CommentData to a minified version
    /// </summary>
    private static MinifiedCommentData MinifyComment(CommentData comment)
    {
        return new MinifiedCommentData
        {
            Author = comment.Author,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            Score = comment.Score,
            Replies = MinifyComments(comment.Replies)
        };
    }

    /// <summary>
    /// Converts minified comment threads to a text format optimized for AI analysis
    /// </summary>
    public static string ConvertMinifiedThreadsToText(List<MinifiedCommentThread> threads)
    {
        if (!threads.Any())
            return string.Empty;

        var result = new StringBuilder();

        foreach (var thread in threads)
        {
            result.AppendLine($"# {thread.Title}");
            if (!string.IsNullOrEmpty(thread.Description))
            {
                result.AppendLine($"Description: {thread.Description}");
            }
            result.AppendLine($"Author: {thread.Author}");
            result.AppendLine($"Created: {thread.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            result.AppendLine($"Source: {thread.Platform}");
            result.AppendLine();

            if (thread.Comments.Any())
            {
                result.AppendLine("## Comments");
                AppendMinifiedCommentsToText(result, thread.Comments, 0);
            }

            result.AppendLine("---");
        }

        return result.ToString();
    }

    /// <summary>
    /// Recursively appends minified comments to a text format for AI analysis
    /// </summary>
    private static void AppendMinifiedCommentsToText(StringBuilder result, List<MinifiedCommentData> comments, int depth)
    {
        foreach (var comment in comments)
        {
            var indent = new string(' ', depth * 2);
            result.AppendLine($"{indent}**{comment.Author}** ({comment.CreatedAt:yyyy-MM-dd HH:mm:ss}):");
            result.AppendLine($"{indent}{comment.Content}");
            if (comment.Score.HasValue)
            {
                result.AppendLine($"{indent}Score: {comment.Score}");
            }
            result.AppendLine();

            if (comment.Replies.Any())
            {
                AppendMinifiedCommentsToText(result, comment.Replies, depth + 1);
            }
        }
    }
}
