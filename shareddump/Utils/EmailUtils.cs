using System.Text;
using Markdig;
using SharedDump.Models.Reddit;

namespace SharedDump.Utils;

public record TopCommentInfo(RedditCommentModel Comment, RedditThreadModel Thread, string CommentUrl);

public static class EmailUtils
{
    private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static string ConvertMarkdownToHtml(string markdown)
    {
        return Markdown.ToHtml(markdown, _markdownPipeline);
    }

    public static string GenerateRedditReportEmail(
        string subreddit, 
        DateTimeOffset cutoffDate, 
        string weeklyAnalysis, 
        List<(RedditThreadModel Thread, string Analysis)> threadAnalyses,
        List<TopCommentInfo> topComments)
    {
        var emailBuilder = new StringBuilder();
        emailBuilder.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Weekly r/" + subreddit + @" Report</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px; }
        .header { background-color: #FF4500; color: white; padding: 20px; border-radius: 5px; margin-bottom: 20px; }
        .section { margin-bottom: 30px; }
        .thread { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 15px; }
        .thread-title { color: #1a1a1b; text-decoration: none; font-weight: bold; }
        .thread-stats { color: #7c7c7c; font-size: 0.9em; }
        .analysis { background-color: #fff; padding: 15px; border-left: 4px solid #FF4500; margin: 10px 0; }
        .top-comment { background-color: #f0f8ff; padding: 15px; border-radius: 5px; margin-bottom: 10px; }
        .comment-meta { color: #7c7c7c; font-size: 0.9em; margin-bottom: 5px; }
        .divider { border-top: 1px solid #eee; margin: 20px 0; }
        .toc { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .toc a { color: #1a1a1b; text-decoration: none; }
        .toc a:hover { text-decoration: underline; }
        .top-posts { background-color: #fff3e0; padding: 15px; border-radius: 5px; margin: 20px 0; }
    </style>
</head>
<body>");

        // Header
        emailBuilder.AppendFormat(@"
    <div class='header'>
        <h1>Weekly r/<a href='https://reddit.com/r/{0}' style='color: white; text-decoration: none;'>{0}</a> Report</h1>
        <p>Analysis for {1:MMMM dd, yyyy} - {2:MMMM dd, yyyy}</p>
    </div>", subreddit, cutoffDate, DateTimeOffset.UtcNow);

        // Top Posts Quick Links
        emailBuilder.AppendLine(@"
    <div class='top-posts'>
        <h2>?? Top Posts This Week</h2>
        <ul>");
        foreach (var (thread, _) in threadAnalyses)
        {
            emailBuilder.AppendFormat(@"
            <li><a href='{0}'>{1}</a> ({2:n0} points, {3:n0} comments)</li>",
                thread.Url,
                thread.Title,
                thread.Score,
                thread.NumComments);
        }
        emailBuilder.AppendLine(@"
        </ul>
    </div>");

        // Table of Contents
        emailBuilder.AppendLine(@"
    <div class='toc'>
        <h2>?? Table of Contents</h2>
        <ul>
            <li><a href='#weekly-summary'>Weekly Summary</a></li>
            <li><a href='#top-comments'>Top Comments</a></li>
            <li><a href='#top-discussions'>Top Discussions</a></li>
        </ul>
    </div>");

        // Weekly Analysis Section
        emailBuilder.AppendLine(@"
    <div class='section' id='weekly-summary'>
        <h2>Weekly Summary</h2>
        <div class='analysis'>");
        emailBuilder.AppendLine(ConvertMarkdownToHtml(weeklyAnalysis));
        emailBuilder.AppendLine(@"
        </div>
    </div>");

        // Top Comments Section
        emailBuilder.AppendLine(@"
    <div class='section' id='top-comments'>
        <h2>Top Comments This Week</h2>");
        foreach (var comment in topComments)
        {
            emailBuilder.AppendFormat(@"
        <div class='top-comment'>
            <div class='comment-meta'>
                <strong><a href='https://reddit.com/user/{0}' style='color: #1a1a1b; text-decoration: none;'>u/{0}</a></strong> · {1:n0} points
                {2}
            </div>
            <div class='comment-content'>
                {3}
            </div>
        </div>", 
                comment.Comment.Author, 
                comment.Comment.Score,
                !string.IsNullOrEmpty(comment.Comment.ParentId) ? $@" · <a href='https://reddit.com/comments/{comment.Comment.ParentId}' style='color: #1a1a1b; text-decoration: none;'>View Thread</a>" : "",
                ConvertMarkdownToHtml(comment.Comment.Body));
        }
        emailBuilder.AppendLine(@"
    </div>");

        // Top Discussions Section
        emailBuilder.AppendLine(@"
    <div class='section' id='top-discussions'>
        <h2>Top Discussions</h2>");
        foreach (var (thread, analysis) in threadAnalyses)
        {
            emailBuilder.AppendFormat(@"
        <div class='thread'>
            <a href='{0}' class='thread-title'>{1}</a>
            <div class='thread-stats'>
                {2:n0} points · {3:n0} comments · Posted by <a href='https://reddit.com/user/{4}' style='color: #7c7c7c; text-decoration: none;'>u/{4}</a>
            </div>
            <div class='analysis'>
                {5}
            </div>
        </div>",
                thread.Url,
                thread.Title,
                thread.Score,
                thread.NumComments,
                thread.Author,
                ConvertMarkdownToHtml(analysis));
        }
        emailBuilder.AppendLine(@"
    </div>");

        // Footer
        emailBuilder.AppendLine(@"
    <div class='divider'></div>
    <div style='text-align: center; color: #7c7c7c; font-size: 0.9em;'>
        Generated by FeedbackFlow
    </div>
</body>
</html>");

        return emailBuilder.ToString();
    }
}