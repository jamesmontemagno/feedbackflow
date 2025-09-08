using System.Text;
using Markdig;
using SharedDump.Models.Reddit;
using SharedDump.Models.Admin;

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
        <p>Analysis for {1:MMMM dd, yyyy} - {2:MMMM dd, yyyy} • {4:n0} total members</p>
        <a href='https://www.feedbackflow.app/report/{3}' class='feedback-button' style='background-color: white; color: #FF4500;'>Share report</a>
    </div>", subreddit, cutoffDate, DateTimeOffset.UtcNow, reportId, subredditInfo.Subscribers);

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
        </div>
        <p style='margin-top: 15px; color: #666; font-size: 0.9em;'><strong>{4}</strong> - {5}</p>
    </div>", 
            subreddit, newThreadsCount, totalCommentsCount, totalUpvotes, 
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

    /// <summary>
    /// Generates a concise Reddit report summary with stats, top posts, and weekly summary only
    /// </summary>
    public static string GenerateRedditReportSummary(
        string subreddit, 
        DateTimeOffset cutoffDate, 
        string weeklyAnalysis, 
        List<(RedditThreadModel Thread, string Analysis)> threadAnalyses,
        string reportId,
        RedditSubredditInfo subredditInfo,
        int newThreadsCount,
        int totalCommentsCount,
        int totalUpvotes,
        string fullReportUrl)
    {
        var emailBuilder = new StringBuilder();
        emailBuilder.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>r/" + subreddit + @" Summary Report</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px; }
        .header { background-color: #FF4500; color: white; padding: 20px; border-radius: 5px; margin-bottom: 20px; }
        .section { margin-bottom: 30px; }
        .analysis { background-color: #fff; padding: 15px; border-left: 4px solid #FF4500; margin: 10px 0; }
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
        .full-report-cta {
            background-color: #e8f4fd;
            border: 2px solid #0366d6;
            border-radius: 8px;
            padding: 20px;
            text-align: center;
            margin: 30px 0;
        }
        .full-report-button {
            display: inline-block;
            background-color: #0366d6;
            color: white;
            padding: 12px 24px;
            border-radius: 6px;
            text-decoration: none;
            font-weight: bold;
            font-size: 1.1em;
            margin-top: 10px;
        }
        .full-report-button:hover {
            background-color: #0255b3;
        }
    </style>
</head>
<body>");

        // Header
        emailBuilder.AppendFormat(@"    <div class='header'>
        <h1>📊 r/<a href='https://reddit.com/r/{0}' style='color: white; text-decoration: none;'>{0}</a> Summary</h1>
        <p>Quick insights for {1:MMMM dd, yyyy} - {2:MMMM dd, yyyy} • {4:n0} total members</p>
        <div style='margin-top: 15px;'>
            <a href='{5}' class='feedback-button' style='background-color: white; color: #FF4500;'>📖 View Full Report</a>
        </div>
    </div>", subreddit, cutoffDate, DateTimeOffset.UtcNow, reportId, subredditInfo.Subscribers, fullReportUrl);

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
        </div>
        <p style='margin-top: 15px; color: #666; font-size: 0.9em;'><strong>{4}</strong> - {5}</p>
    </div>", 
            subreddit, newThreadsCount, totalCommentsCount, totalUpvotes, 
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

        // Weekly Analysis Section
        emailBuilder.AppendLine(@"
    <div class='section' id='weekly-summary'>
        <h2>📝 Weekly Summary</h2>
        <div class='analysis'>");
        emailBuilder.AppendLine(ConvertMarkdownToHtml(weeklyAnalysis));
        emailBuilder.AppendLine(@"
        </div>
    </div>");

        // Full Report CTA
        emailBuilder.AppendFormat(@"
    <div class='full-report-cta'>
        <h3>🔍 Want More Details?</h3>
        <p>This is a summary report. For detailed analysis including top comments and full thread discussions, check out the complete report.</p>
        <a href='{0}' class='full-report-button'>View Full Report</a>
    </div>", fullReportUrl);

        // Footer
        emailBuilder.AppendFormat(@"
    <div style='text-align: center; color: #7c7c7c; font-size: 0.9em; margin-top: 30px; border-top: 1px solid #eee; padding-top: 20px;'>
        <div style='margin-bottom: 15px;'>
            <a href='{0}' class='feedback-button' style='background-color: #0366d6; color: white;'>📖 View Full Report</a>
        </div>
        <p>Generated by FeedbackFlow 🥰</p>
    </div>
</body>
</html>", fullReportUrl);

        return emailBuilder.ToString();
    }

    /// <summary>
    /// Generates a comprehensive weekly admin report email with dashboard statistics, charts, and metrics
    /// </summary>
    public static string GenerateAdminWeeklyReportEmail(
        string recipientName,
        AdminDashboardMetrics metrics,
        DateTime weekEnding,
        int totalActiveReportConfigs,
        int reportsGeneratedThisWeek,
        List<string> topActiveRepositories,
        List<string> topActiveSubreddits)
    {
        var emailBuilder = new StringBuilder();
        var weekEndingFormatted = weekEnding.ToString("MMMM d, yyyy");
        
        emailBuilder.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Weekly Admin Report - FeedbackFlow</title>
    <style>
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            line-height: 1.6; 
            color: #333; 
            max-width: 900px; 
            margin: 0 auto; 
            padding: 20px; 
            background-color: #f8f9fa;
        }
        .email-container {
            background-color: white;
            border-radius: 10px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            overflow: hidden;
        }
        .header { 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
            color: white; 
            padding: 30px; 
            text-align: center;
        }
        .header h1 {
            margin: 0;
            font-size: 2.2em;
            font-weight: 300;
        }
        .header p {
            margin: 10px 0 0 0;
            opacity: 0.9;
            font-size: 1.1em;
        }
        .content {
            padding: 30px;
        }
        .section { 
            margin-bottom: 40px; 
        }
        .section h2 {
            color: #667eea;
            border-bottom: 2px solid #f1f3f4;
            padding-bottom: 10px;
            margin-bottom: 20px;
            font-size: 1.4em;
        }
        .stats-grid { 
            display: grid; 
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); 
            gap: 20px; 
            margin: 20px 0; 
        }
        .stat-card { 
            background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
            padding: 25px; 
            border-radius: 12px; 
            text-align: center; 
            border: 1px solid #dee2e6;
            transition: transform 0.2s ease;
        }
        .stat-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 15px rgba(0,0,0,0.1);
        }
        .stat-number { 
            font-size: 2.5em; 
            font-weight: bold; 
            color: #667eea; 
            display: block; 
            margin-bottom: 5px;
        }
        .stat-label { 
            color: #6c757d; 
            font-size: 0.9em; 
            text-transform: uppercase;
            letter-spacing: 0.5px;
            font-weight: 600;
        }
        .stat-change {
            font-size: 0.8em;
            margin-top: 5px;
        }
        .stat-change.positive {
            color: #28a745;
        }
        .stat-change.neutral {
            color: #6c757d;
        }
        .chart-container {
            background-color: #f8f9fa;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
        }
        .chart-bar {
            display: flex;
            align-items: center;
            margin-bottom: 15px;
            padding: 10px;
            background: white;
            border-radius: 6px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
        }
        .chart-label {
            min-width: 120px;
            font-weight: 600;
            color: #495057;
        }
        .chart-bar-fill {
            height: 20px;
            background: linear-gradient(90deg, #667eea, #764ba2);
            border-radius: 10px;
            margin: 0 15px;
            flex: 1;
            position: relative;
        }
        .chart-value {
            font-weight: bold;
            color: #667eea;
            min-width: 60px;
            text-align: right;
        }
        .tier-distribution {
            display: flex;
            gap: 15px;
            flex-wrap: wrap;
            margin: 20px 0;
        }
        .tier-item {
            flex: 1;
            min-width: 120px;
            text-align: center;
            padding: 15px;
            background: white;
            border-radius: 8px;
            border: 2px solid #e9ecef;
        }
        .tier-bronze { border-color: #cd7f32; }
        .tier-silver { border-color: #c0c0c0; }
        .tier-gold { border-color: #ffd700; }
        .tier-admin { border-color: #667eea; }
        .active-sources {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            margin: 20px 0;
        }
        .source-list {
            background: #f8f9fa;
            padding: 20px;
            border-radius: 8px;
        }
        .source-list h4 {
            margin: 0 0 15px 0;
            color: #495057;
        }
        .source-list ul {
            list-style: none;
            padding: 0;
            margin: 0;
        }
        .source-list li {
            padding: 8px 0;
            border-bottom: 1px solid #dee2e6;
            display: flex;
            align-items: center;
        }
        .source-list li:last-child {
            border-bottom: none;
        }
        .source-icon {
            margin-right: 10px;
            font-size: 1.1em;
        }
        .cta-section {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 12px;
            text-align: center;
            margin-top: 30px;
        }
        .cta-button {
            display: inline-block;
            background-color: white;
            color: #667eea;
            padding: 12px 30px;
            border-radius: 25px;
            text-decoration: none;
            font-weight: bold;
            margin-top: 15px;
            transition: transform 0.2s ease;
        }
        .cta-button:hover {
            transform: translateY(-2px);
            text-decoration: none;
            color: #667eea;
        }
        .footer {
            text-align: center;
            padding: 20px;
            color: #6c757d;
            font-size: 0.9em;
            border-top: 1px solid #dee2e6;
        }
        @media (max-width: 600px) {
            .stats-grid {
                grid-template-columns: 1fr;
            }
            .active-sources {
                grid-template-columns: 1fr;
            }
            .tier-distribution {
                flex-direction: column;
            }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>📊 Weekly Admin Report</h1>
            <p>Week ending " + weekEndingFormatted + @"</p>
            <p>Hello " + recipientName + @",</p>
        </div>
        
        <div class='content'>
            <!-- Executive Summary -->
            <div class='section'>
                <h2>🎯 Executive Summary</h2>
                <div class='stats-grid'>
                    <div class='stat-card'>
                        <span class='stat-number'>" + metrics.UserStats.TotalUsers.ToString("N0") + @"</span>
                        <div class='stat-label'>Total Users</div>");
        
        if (metrics.UserStats.NewUsersLast7Days > 0)
        {
            emailBuilder.AppendLine(@"
                        <div class='stat-change positive'>+" + metrics.UserStats.NewUsersLast7Days + @" this week</div>");
        }
        
        emailBuilder.AppendLine(@"
                    </div>
                    <div class='stat-card'>
                        <span class='stat-number'>" + metrics.UserStats.ActiveUsersLast14Days.ToString("N0") + @"</span>
                        <div class='stat-label'>Active Users (14d)</div>
                        <div class='stat-change neutral'>" + metrics.UserStats.ActiveUsersPercentage.ToString("F1") + @"% of total</div>
                    </div>
                    <div class='stat-card'>
                        <span class='stat-number'>" + totalActiveReportConfigs.ToString("N0") + @"</span>
                        <div class='stat-label'>Active Report Configs</div>
                    </div>
                    <div class='stat-card'>
                        <span class='stat-number'>" + reportsGeneratedThisWeek.ToString("N0") + @"</span>
                        <div class='stat-label'>Reports Generated</div>
                        <div class='stat-change neutral'>This week</div>
                    </div>
                </div>
            </div>

            <!-- User Distribution -->
            <div class='section'>
                <h2>👥 User Tier Distribution</h2>
                <div class='tier-distribution'>");

        foreach (var tier in metrics.UserStats.TierDistribution)
        {
            var tierClass = tier.Key.ToLower() switch
            {
                "bronze" => "tier-bronze",
                "silver" => "tier-silver", 
                "gold" => "tier-gold",
                "admin" => "tier-admin",
                _ => ""
            };
            
            emailBuilder.AppendLine($@"
                    <div class='tier-item {tierClass}'>
                        <div class='stat-number' style='font-size: 1.8em;'>{tier.Value:N0}</div>
                        <div class='stat-label'>{tier.Key}</div>
                        <div class='stat-change neutral'>{metrics.UserStats.TierDistributionPercentage.GetValueOrDefault(tier.Key, 0):F1}%</div>
                    </div>");
        }

        emailBuilder.AppendLine(@"
                </div>
            </div>

            <!-- Usage Analytics -->
            <div class='section'>
                <h2>📈 Usage Analytics</h2>
                <div class='chart-container'>
                    <div class='chart-bar'>
                        <div class='chart-label'>AI Analyses</div>
                        <div class='chart-bar-fill' style='width: 100%;'></div>
                        <div class='chart-value'>" + metrics.UsageStats.TotalAnalysesUsed.ToString("N0") + @"</div>
                    </div>
                    <div class='chart-bar'>
                        <div class='chart-label'>Feed Queries</div>
                        <div class='chart-bar-fill' style='width: " + (metrics.UsageStats.TotalFeedQueriesUsed > 0 ? Math.Min(100, (double)metrics.UsageStats.TotalFeedQueriesUsed / metrics.UsageStats.TotalAnalysesUsed * 100) : 0).ToString("F0") + @"%;'></div>
                        <div class='chart-value'>" + metrics.UsageStats.TotalFeedQueriesUsed.ToString("N0") + @"</div>
                    </div>
                    <div class='chart-bar'>
                        <div class='chart-label'>API Calls</div>
                        <div class='chart-bar-fill' style='width: " + (metrics.UsageStats.TotalApiCalls > 0 ? Math.Min(100, (double)metrics.UsageStats.TotalApiCalls / metrics.UsageStats.TotalAnalysesUsed * 100) : 0).ToString("F0") + @"%;'></div>
                        <div class='chart-value'>" + metrics.UsageStats.TotalApiCalls.ToString("N0") + @"</div>
                    </div>
                </div>
            </div>

            <!-- API Adoption -->
            <div class='section'>
                <h2>🔧 API Adoption</h2>
                <div class='stats-grid'>
                    <div class='stat-card'>
                        <span class='stat-number'>" + metrics.ApiStats.TotalApiEnabledUsers.ToString("N0") + @"</span>
                        <div class='stat-label'>API Enabled Users</div>
                        <div class='stat-change neutral'>" + ((double)metrics.ApiStats.TotalApiEnabledUsers / metrics.UserStats.TotalUsers * 100).ToString("F1") + @"% adoption</div>
                    </div>
                    <div class='stat-card'>
                        <span class='stat-number'>" + metrics.ApiStats.AverageApiCallsPerUser.ToString("F1") + @"</span>
                        <div class='stat-label'>Avg Calls/User</div>
                    </div>
                </div>
            </div>");

        // Active Sources Section
        if (topActiveRepositories.Any() || topActiveSubreddits.Any())
        {
            emailBuilder.AppendLine(@"
            <!-- Active Sources -->
            <div class='section'>
                <h2>🚀 Most Active Sources</h2>
                <div class='active-sources'>");

            if (topActiveRepositories.Any())
            {
                emailBuilder.AppendLine(@"
                    <div class='source-list'>
                        <h4>🐙 GitHub Repositories</h4>
                        <ul>");
                foreach (var repo in topActiveRepositories.Take(5))
                {
                    emailBuilder.AppendLine($@"
                            <li><span class='source-icon'>📁</span>{repo}</li>");
                }
                emailBuilder.AppendLine(@"
                        </ul>
                    </div>");
            }

            if (topActiveSubreddits.Any())
            {
                emailBuilder.AppendLine(@"
                    <div class='source-list'>
                        <h4>🔥 Reddit Communities</h4>
                        <ul>");
                foreach (var subreddit in topActiveSubreddits.Take(5))
                {
                    emailBuilder.AppendLine($@"
                            <li><span class='source-icon'>💬</span>r/{subreddit}</li>");
                }
                emailBuilder.AppendLine(@"
                        </ul>
                    </div>");
            }

            emailBuilder.AppendLine(@"
                </div>
            </div>");
        }

        emailBuilder.AppendLine(@"
            <!-- Call to Action -->
            <div class='cta-section'>
                <h3 style='margin: 0 0 10px 0;'>🎯 Ready to dive deeper?</h3>
                <p style='margin: 0 0 15px 0; opacity: 0.9;'>Access the full admin dashboard for detailed insights and management tools.</p>
                <a href='/admin/dashboard' class='cta-button'>View Full Dashboard</a>
            </div>
        </div>
        
        <div class='footer'>
            <p>Generated by FeedbackFlow Admin Analytics • " + DateTime.UtcNow.ToString("MMMM d, yyyy 'at' h:mm tt") + @" UTC</p>
            <p style='margin: 5px 0 0 0; font-size: 0.8em; opacity: 0.8;'>This is an automated weekly summary for FeedbackFlow administrators.</p>
        </div>
    </div>
</body>
</html>");

        return emailBuilder.ToString();
    }
}
