using System.Text;
using System.Text.Json;
using SharedDump.Models;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Models.DevBlogs;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.Reddit;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.YouTube;

namespace SharedDump.Services;

/// <summary>
/// Prepares comments for AI analysis by limiting count and converting to an optimized text format.
/// This reduces token usage and HTTP payload size significantly compared to sending raw JSON.
/// </summary>
public static class CommentPreparer
{
    /// <summary>
    /// Prepares comments for analysis by converting platform-specific data to optimized text format.
    /// This significantly reduces payload size compared to raw JSON.
    /// </summary>
    /// <param name="additionalData">Platform-specific data object from feedback services</param>
    /// <param name="maxComments">Maximum number of comments to include</param>
    /// <param name="useSlimmedFormat">If true, excludes metadata like IDs, URLs, scores for minimal token usage</param>
    /// <returns>Tuple of (prepared text for analysis, actual comment count included)</returns>
    public static (string text, int commentCount) PrepareForAnalysis(
        object? additionalData, 
        int maxComments = 500, 
        bool useSlimmedFormat = true)
    {
        if (additionalData is null)
            return (string.Empty, 0);

        var threads = ConvertToThreads(additionalData, useSlimmedFormat);
        if (!threads.Any())
        {
            // Log what type we received that didn't match
            System.Diagnostics.Debug.WriteLine($"CommentPreparer: No threads converted from type {additionalData.GetType().FullName}");
            return (string.Empty, 0);
        }

        return ConvertThreadsToText(threads, maxComments, useSlimmedFormat);
    }

    /// <summary>
    /// Converts raw JSON response to optimized text format for analysis.
    /// Attempts to detect the platform type from the JSON structure.
    /// </summary>
    /// <param name="jsonContent">Raw JSON string from backend API</param>
    /// <param name="serviceType">Platform type hint (github, reddit, hackernews, etc.)</param>
    /// <param name="maxComments">Maximum number of comments to include</param>
    /// <param name="useSlimmedFormat">If true, excludes metadata for minimal token usage</param>
    /// <returns>Tuple of (prepared text for analysis, actual comment count included)</returns>
    public static (string text, int commentCount) PrepareJsonForAnalysis(
        string jsonContent,
        string serviceType,
        int maxComments = 500,
        bool useSlimmedFormat = true)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return (string.Empty, 0);

        try
        {
            var data = DeserializeByServiceType(jsonContent, serviceType);
            if (data is null)
                return (jsonContent, 0); // Fallback to raw JSON if parsing fails

            return PrepareForAnalysis(data, maxComments, useSlimmedFormat);
        }
        catch (JsonException)
        {
            // If deserialization fails, return original content
            return (jsonContent, 0);
        }
    }

    private static object? DeserializeByServiceType(string jsonContent, string serviceType)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        return serviceType.ToLowerInvariant() switch
        {
            "github" => TryDeserializeGitHub(jsonContent, options),
            "reddit" => JsonSerializer.Deserialize<List<RedditThreadModel>>(jsonContent, options)?.FirstOrDefault(),
            "hackernews" => JsonSerializer.Deserialize<List<List<HackerNewsItem>>>(jsonContent, options)?.FirstOrDefault(),
            "youtube" => JsonSerializer.Deserialize<List<YouTubeOutputVideo>>(jsonContent, options),
            "bluesky" => JsonSerializer.Deserialize<BlueSkyFeedbackResponse>(jsonContent, options),
            "twitter" => JsonSerializer.Deserialize<TwitterFeedbackResponse>(jsonContent, options),
            "devblogs" => JsonSerializer.Deserialize<DevBlogsArticleModel>(jsonContent, options),
            _ => null
        };
    }

    private static object? TryDeserializeGitHub(string jsonContent, JsonSerializerOptions options)
    {
        // Try issues first
        try
        {
            var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(jsonContent, options);
            if (issues is { Count: > 0 })
                return issues;
        }
        catch { /* Try discussions next */ }

        // Try discussions
        try
        {
            var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(jsonContent, options);
            if (discussions is { Count: > 0 })
                return discussions;
        }
        catch { /* Return null */ }

        return null;
    }

    private static List<CommentThread> ConvertToThreads(object additionalData, bool forAnalysis)
    {
        return additionalData switch
        {
            List<YouTubeOutputVideo> videos => CommentDataConverter.ConvertYouTube(videos, forAnalysis),
            RedditThreadModel thread => new List<CommentThread> { CommentDataConverter.ConvertReddit(thread, forAnalysis) },
            List<RedditThreadModel> threads => CommentDataConverter.ConvertReddit(threads, forAnalysis),
            List<GithubIssueModel> issues => CommentDataConverter.ConvertGitHubIssues(issues, forAnalysis),
            List<GithubDiscussionModel> discussions => CommentDataConverter.ConvertGitHubDiscussions(discussions, forAnalysis),
            DevBlogsArticleModel article => CommentDataConverter.ConvertDevBlogs(article, forAnalysis),
            BlueSkyFeedbackResponse response => CommentDataConverter.ConvertBlueSky(response, forAnalysis),
            TwitterFeedbackResponse response => CommentDataConverter.ConvertTwitter(response, forAnalysis),
            List<HackerNewsItem> items => CommentDataConverter.ConvertHackerNews(items, forAnalysis),
            _ => new List<CommentThread>()
        };
    }

    private static (string text, int commentCount) ConvertThreadsToText(
        List<CommentThread> threads, 
        int maxComments, 
        bool useSlimmedFormat)
    {
        if (!threads.Any())
            return (string.Empty, 0);

        var result = new StringBuilder();
        var commentCount = 0;
        var remainingComments = maxComments;

        foreach (var thread in threads)
        {
            if (remainingComments <= 0)
                break;

            // Add thread header
            result.AppendLine($"# {thread.Title}");
            
            if (!string.IsNullOrEmpty(thread.Description))
            {
                result.AppendLine();
                result.AppendLine(thread.Description);
            }
            
            if (!useSlimmedFormat)
            {
                result.AppendLine();
                result.AppendLine($"Author: {thread.Author}");
                result.AppendLine($"Created: {thread.CreatedAt:yyyy-MM-dd HH:mm}");
                result.AppendLine($"Source: {thread.SourceType}");
                if (!string.IsNullOrEmpty(thread.Url))
                    result.AppendLine($"URL: {thread.Url}");
            }
            
            result.AppendLine();

            if (thread.Comments.Any())
            {
                result.AppendLine("## Comments");
                result.AppendLine();
                
                var (commentsText, count) = AppendComments(thread.Comments, 0, remainingComments, useSlimmedFormat);
                result.Append(commentsText);
                commentCount += count;
                remainingComments -= count;
            }

            result.AppendLine("---");
            result.AppendLine();
        }

        if (remainingComments <= 0 && threads.Sum(t => CountTotalComments(t.Comments)) > maxComments)
        {
            result.AppendLine($"_Note: Analysis limited to {maxComments} comments for optimal performance._");
        }

        return (result.ToString(), commentCount);
    }

    private static (string text, int count) AppendComments(
        List<CommentData> comments, 
        int depth, 
        int remaining,
        bool useSlimmedFormat)
    {
        var result = new StringBuilder();
        var count = 0;

        foreach (var comment in comments)
        {
            if (remaining <= 0)
                break;

            var indent = new string(' ', depth * 2);
            
            if (useSlimmedFormat)
            {
                // Minimal format: just author and content
                result.AppendLine($"{indent}**{comment.Author}**:");
                result.AppendLine($"{indent}{comment.Content}");
            }
            else
            {
                // Full format with metadata
                result.AppendLine($"{indent}**{comment.Author}** ({comment.CreatedAt:yyyy-MM-dd HH:mm}):");
                result.AppendLine($"{indent}{comment.Content}");
                if (comment.Score.HasValue && comment.Score > 0)
                    result.AppendLine($"{indent}_Score: {comment.Score}_");
            }
            
            result.AppendLine();
            count++;
            remaining--;

            if (comment.Replies.Any() && remaining > 0)
            {
                var (repliesText, repliesCount) = AppendComments(comment.Replies, depth + 1, remaining, useSlimmedFormat);
                result.Append(repliesText);
                count += repliesCount;
                remaining -= repliesCount;
            }
        }

        return (result.ToString(), count);
    }

    private static int CountTotalComments(List<CommentData> comments)
    {
        var count = comments.Count;
        foreach (var comment in comments)
        {
            count += CountTotalComments(comment.Replies);
        }
        return count;
    }

    /// <summary>
    /// Estimates the byte size reduction when using prepared text vs raw JSON.
    /// Useful for displaying to users.
    /// </summary>
    public static (int originalBytes, int preparedBytes, double reductionPercent) EstimateReduction(
        string originalJson,
        string preparedText)
    {
        var originalBytes = Encoding.UTF8.GetByteCount(originalJson);
        var preparedBytes = Encoding.UTF8.GetByteCount(preparedText);
        var reductionPercent = originalBytes > 0 
            ? Math.Round((1 - (double)preparedBytes / originalBytes) * 100, 1) 
            : 0;
        
        return (originalBytes, preparedBytes, reductionPercent);
    }
}
