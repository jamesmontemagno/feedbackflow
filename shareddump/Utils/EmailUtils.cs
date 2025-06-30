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
    }    public static string GenerateRedditReportEmail(
        string subreddit, 
        DateTimeOffset cutoffDate, 
        string weeklyAnalysis, 
        List<(RedditThreadModel Thread, string Analysis)> threadAnalyses,
        List<TopCommentInfo> topComments,
        string reportId,
        RedditSubredditInfo subredditInfo,
        int newThreadsCount,
        int totalCommentsCount,
        int totalUpvotes)
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
        .toc ul { list-style-type: none; padding-left: 20px; }
        .toc > ul { padding-left: 0; }
        .toc-section { font-weight: bold; margin-top: 10px; }
        .toc-subsection { padding-left: 20px; }
        .top-posts { background-color: #fff3e0; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .feedback-button { 
            display: inline-block; 
            background-color: #0366d6; 
            color: white; 
            padding: 8px 16px; 
            border-radius: 4px; 
            text-decoration: none; 
            margin-top: 10px;
            font-size: 0.9em;
        }
        .feedback-button:hover {
            background-color: #0255b3;
        }
        .action-buttons {
            display: flex;
            gap: 10px;
            margin-top: 10px;
        }
        .stats-section { 
            background-color: #f8f9fa; 
            padding: 20px; 
            border-radius: 5px; 
            margin: 20px 0; 
            border-left: 4px solid #FF4500; 
        }
        .stats-grid { 
            display: grid; 
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); 
            gap: 15px; 
            margin-top: 15px; 
        }
        .stat-item { 
            background-color: white; 
            padding: 15px; 
            border-radius: 5px; 
            text-align: center; 
            box-shadow: 0 2px 4px rgba(0,0,0,0.1); 
        }
        .stat-number { 
            font-size: 2em; 
            font-weight: bold; 
            color: #FF4500; 
            display: block; 
        }
        .stat-label { 
            color: #666; 
            font-size: 0.9em; 
            margin-top: 5px; 
        }
    </style>
</head>
<body>");

        // Header
        emailBuilder.AppendFormat(@"    <div class='header'>
        <h1>Weekly r/<a href='https://reddit.com/r/{0}' style='color: white; text-decoration: none;'>{0}</a> Report</h1>
        <p>Analysis for {1:MMMM dd, yyyy} - {2:MMMM dd, yyyy}</p>
        <a href='https://www.feedbackflow.app/report/{3}' class='feedback-button' style='background-color: white; color: #FF4500;'>Share report</a>
    </div>", subreddit, cutoffDate, DateTimeOffset.UtcNow, reportId);

        // Subreddit Stats Section
        emailBuilder.AppendFormat(@"
    <div class='stats-section'>
        <h2>📊 r/{0} Statistics</h2>
        <div class='stats-grid'>
            <div class='stat-item'>
                <span class='stat-number'>{1:n0}</span>
                <div class='stat-label'>📝 New Threads (7 days)</div>
            </div>
            <div class='stat-item'>
                <span class='stat-number'>{2:n0}</span>
                <div class='stat-label'>💬 Total Comments</div>
            </div>
            <div class='stat-item'>
                <span class='stat-number'>{3:n0}</span>
                <div class='stat-label'>⬆️ Total Upvotes</div>
            </div>
            <div class='stat-item'>
                <span class='stat-number'>{4:n0}</span>
                <div class='stat-label'>👥 Subscribers</div>
            </div>
            <div class='stat-item'>
                <span class='stat-number'>{5:n0}</span>
                <div class='stat-label'>🟢 Active Users</div>
            </div>
        </div>
        <p style='margin-top: 15px; color: #666; font-size: 0.9em;'><strong>{6}</strong> - {7}</p>
    </div>", 
            subreddit, newThreadsCount, totalCommentsCount, totalUpvotes, 
            subredditInfo.Subscribers, subredditInfo.AccountsActive,
            subredditInfo.Title, subredditInfo.PublicDescription);

        // Top Posts Quick Links
        emailBuilder.AppendLine(@"
    <div class='top-posts'>
        <h2>🔝 Top Posts This Week</h2>
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
        <h2>📖 Table of Contents</h2>
        <ul>
            <li class='toc-section'><a href='#weekly-summary'>Weekly Summary</a></li>
            <li class='toc-section'><a href='#top-comments'>Top Comments</a></li>
            <li class='toc-section'><a href='#top-discussions'>Top Discussions</a>
                <ul class='toc-subsection'>");

        foreach (var (thread, _) in threadAnalyses)
        {
            var threadId = $"thread-{thread.Id}";
            emailBuilder.AppendFormat(@"
                    <li><a href='#{0}'>{1}</a></li>", 
                threadId, 
                thread.Title.Length > 60 ? thread.Title[..57] + "..." : thread.Title);
        }

        emailBuilder.AppendLine(@"
                </ul>
            </li>
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
                <strong><a href='https://reddit.com/user/{0}' style='color: #1a1a1b; text-decoration: none;'>u/{0}</a></strong> · {1:n0} points · 
                <a href='{2}' style='color: #1a1a1b; text-decoration: none;'>View in Thread: {3}</a>
            </div>
            <div class='comment-content'>
                {4}
            </div>
        </div>", 
                comment.Comment.Author, 
                comment.Comment.Score,
                comment.CommentUrl,
                comment.Thread.Title.Length > 60 ? comment.Thread.Title[..57] + "..." : comment.Thread.Title,
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
            var threadId = $"thread-{thread.Id}";
            emailBuilder.AppendFormat(@"
        <div class='thread' id='{0}'>
            <a href='{1}' class='thread-title'>{2}</a>
            <div class='thread-stats'>
                {3:n0} points · {4:n0} comments · Posted by <a href='https://reddit.com/user/{5}' style='color: #7c7c7c; text-decoration: none;'>u/{5}</a>
            </div>
            <div class='action-buttons'>
                <a href='{1}' class='feedback-button'>View on Reddit</a>
                <a href='https://www.feedbackflow.app/?source=auto&id={6}' class='feedback-button'>Open in FeedbackFlow</a>
            </div>
            <div class='analysis'>
                {7}
            </div>
        </div>",
                threadId,
                thread.Url,
                thread.Title,
                thread.Score,
                thread.NumComments,
                thread.Author,
                Uri.EscapeDataString(thread.Url),
                ConvertMarkdownToHtml(analysis));
        }
        emailBuilder.AppendLine(@"
    </div>");

        // Footer
        emailBuilder.AppendLine(@"
    <div class='divider'></div>
    <div style='text-align: center; color: #7c7c7c; font-size: 0.9em;'>
        Generated by FeedbackFlow 🥰
    </div>
</body>
</html>");

        return emailBuilder.ToString();
    }
}
