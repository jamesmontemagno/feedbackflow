using SharedDump.Models;
using SharedDump.Models.HackerNews;
using SharedDump.Services;

namespace FeedbackWebApp.Services.Feedback;

/// <summary>
/// Extension methods for converting HackerNews-specific data to common comment structures
/// </summary>
public static class HackerNewsCommentConverter
{
    /// <summary>
    /// Converts HackerNews analysis data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertHackerNewsAnalysis(List<HackerNewsAnalysis> analyses)
    {
        var threads = new List<CommentThread>();

        foreach (var analysis in analyses)
        {
            // Use the CommentDataConverter from shareddump for the actual conversion
            var convertedThreads = CommentDataConverter.ConvertHackerNews(analysis.Stories);
            threads.AddRange(convertedThreads);
        }

        return threads;
    }
}