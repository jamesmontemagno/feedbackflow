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
    /// Debug log action - can be set by consuming applications to capture debug info
    /// </summary>
    public static Action<string>? DebugLog { get; set; }

    private static void Log(string message)
    {
        DebugLog?.Invoke($"[CommentPreparer] {message}");
        System.Diagnostics.Debug.WriteLine($"[CommentPreparer] {message}");
    }

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
        Log($"PrepareForAnalysis called: maxComments={maxComments}, useSlimmedFormat={useSlimmedFormat}");
        
        if (additionalData is null)
        {
            Log("PrepareForAnalysis: additionalData is null, returning empty");
            return (string.Empty, 0);
        }

        var dataType = additionalData.GetType();
        Log($"PrepareForAnalysis: additionalData type = {dataType.FullName}");
        
        // Log collection info if it's a list
        if (additionalData is System.Collections.ICollection collection)
        {
            Log($"PrepareForAnalysis: collection count = {collection.Count}");
        }

        var threads = ConvertToThreads(additionalData, useSlimmedFormat);
        
        Log($"PrepareForAnalysis: ConvertToThreads returned {threads.Count} threads");
        
        if (!threads.Any())
        {
            Log($"PrepareForAnalysis: No threads converted from type {dataType.FullName} - RETURNING EMPTY");
            return (string.Empty, 0);
        }

        // Log thread details
        foreach (var thread in threads)
        {
            var totalComments = CountTotalComments(thread.Comments);
            Log($"PrepareForAnalysis: Thread '{thread.Title}' has {thread.Comments.Count} top-level comments, {totalComments} total comments");
        }

        var result = ConvertThreadsToText(threads, maxComments, useSlimmedFormat);
        Log($"PrepareForAnalysis: Final result - textLength={result.text.Length}, commentCount={result.commentCount}");
        
        return result;
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
        Log($"PrepareJsonForAnalysis called: serviceType={serviceType}, jsonLength={jsonContent?.Length ?? 0}, maxComments={maxComments}");
        
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            Log("PrepareJsonForAnalysis: jsonContent is empty, returning empty");
            return (string.Empty, 0);
        }

        try
        {
            var data = DeserializeByServiceType(jsonContent, serviceType);
            if (data is null)
            {
                Log($"PrepareJsonForAnalysis: DeserializeByServiceType returned null for serviceType={serviceType}, returning raw JSON");
                return (jsonContent, 0); // Fallback to raw JSON if parsing fails
            }

            Log($"PrepareJsonForAnalysis: Successfully deserialized to type {data.GetType().FullName}");
            return PrepareForAnalysis(data, maxComments, useSlimmedFormat);
        }
        catch (JsonException ex)
        {
            Log($"PrepareJsonForAnalysis: JsonException during deserialization: {ex.Message}");
            // If deserialization fails, return original content
            return (jsonContent, 0);
        }
    }

    private static object? DeserializeByServiceType(string jsonContent, string serviceType)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var normalizedType = serviceType.ToLowerInvariant();
        
        Log($"DeserializeByServiceType: Attempting to deserialize as '{normalizedType}'");
        
        try
        {
            object? result = normalizedType switch
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
            
            Log($"DeserializeByServiceType: Result type = {result?.GetType().FullName ?? "null"}");
            return result;
        }
        catch (Exception ex)
        {
            Log($"DeserializeByServiceType: Exception during deserialization: {ex.Message}");
            return null;
        }
    }

    private static object? TryDeserializeGitHub(string jsonContent, JsonSerializerOptions options)
    {
        // Try issues first
        try
        {
            var issues = JsonSerializer.Deserialize<List<GithubIssueModel>>(jsonContent, options);
            if (issues is { Count: > 0 })
            {
                Log($"TryDeserializeGitHub: Successfully deserialized as {issues.Count} GitHub issues");
                return issues;
            }
        }
        catch (Exception ex) 
        { 
            Log($"TryDeserializeGitHub: Failed to deserialize as issues: {ex.Message}");
        }

        // Try discussions
        try
        {
            var discussions = JsonSerializer.Deserialize<List<GithubDiscussionModel>>(jsonContent, options);
            if (discussions is { Count: > 0 })
            {
                Log($"TryDeserializeGitHub: Successfully deserialized as {discussions.Count} GitHub discussions");
                return discussions;
            }
        }
        catch (Exception ex) 
        { 
            Log($"TryDeserializeGitHub: Failed to deserialize as discussions: {ex.Message}");
        }

        Log("TryDeserializeGitHub: Could not deserialize as issues or discussions");
        return null;
    }

    private static List<CommentThread> ConvertToThreads(object additionalData, bool forAnalysis)
    {
        var typeName = additionalData.GetType().FullName;
        Log($"ConvertToThreads: Checking type patterns for {typeName}");
        
        List<CommentThread> result = additionalData switch
        {
            List<YouTubeOutputVideo> videos => LogAndConvert("YouTube", () => CommentDataConverter.ConvertYouTube(videos, forAnalysis)),
            RedditThreadModel thread => LogAndConvert("Reddit (single)", () => new List<CommentThread> { CommentDataConverter.ConvertReddit(thread, forAnalysis) }),
            List<RedditThreadModel> threads => LogAndConvert("Reddit (list)", () => CommentDataConverter.ConvertReddit(threads, forAnalysis)),
            List<GithubIssueModel> issues => LogAndConvert("GitHub Issues", () => CommentDataConverter.ConvertGitHubIssues(issues, forAnalysis)),
            List<GithubDiscussionModel> discussions => LogAndConvert("GitHub Discussions", () => CommentDataConverter.ConvertGitHubDiscussions(discussions, forAnalysis)),
            DevBlogsArticleModel article => LogAndConvert("DevBlogs", () => CommentDataConverter.ConvertDevBlogs(article, forAnalysis)),
            BlueSkyFeedbackResponse response => LogAndConvert("BlueSky", () => CommentDataConverter.ConvertBlueSky(response, forAnalysis)),
            TwitterFeedbackResponse response => LogAndConvert("Twitter", () => CommentDataConverter.ConvertTwitter(response, forAnalysis)),
            List<HackerNewsItem> items => LogAndConvert("HackerNews", () => CommentDataConverter.ConvertHackerNews(items, forAnalysis)),
            _ => LogNoMatch(typeName)
        };
        
        return result;
    }

    private static List<CommentThread> LogAndConvert(string platform, Func<List<CommentThread>> converter)
    {
        Log($"ConvertToThreads: Matched {platform} pattern, converting...");
        try
        {
            var result = converter();
            Log($"ConvertToThreads: {platform} conversion returned {result.Count} threads");
            return result;
        }
        catch (Exception ex)
        {
            Log($"ConvertToThreads: {platform} conversion threw exception: {ex.Message}");
            return new List<CommentThread>();
        }
    }

    private static List<CommentThread> LogNoMatch(string? typeName)
    {
        Log($"ConvertToThreads: NO PATTERN MATCHED for type {typeName ?? "unknown"}");
        return new List<CommentThread>();
    }

    private static (string text, int commentCount) ConvertThreadsToText(
        List<CommentThread> threads, 
        int maxComments, 
        bool useSlimmedFormat)
    {
        Log($"ConvertThreadsToText: threads={threads.Count}, maxComments={maxComments}, useSlimmedFormat={useSlimmedFormat}");
        
        if (!threads.Any())
            return (string.Empty, 0);

        var result = new StringBuilder();
        var commentCount = 0;
        var remainingComments = maxComments;

        foreach (var thread in threads)
        {
            if (remainingComments <= 0)
            {
                Log($"ConvertThreadsToText: Reached max comments limit, stopping");
                break;
            }

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
                
                Log($"ConvertThreadsToText: Added {count} comments from thread '{thread.Title}', remaining={remainingComments}");
            }

            result.AppendLine("---");
            result.AppendLine();
        }

        if (remainingComments <= 0 && threads.Sum(t => CountTotalComments(t.Comments)) > maxComments)
        {
            result.AppendLine($"_Note: Analysis limited to {maxComments} comments for optimal performance._");
        }

        Log($"ConvertThreadsToText: Final output - length={result.Length}, commentCount={commentCount}");
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
