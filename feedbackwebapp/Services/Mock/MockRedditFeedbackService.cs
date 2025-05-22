using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.Reddit;

namespace FeedbackWebApp.Services.Mock;

public class MockRedditFeedbackService : FeedbackService, IRedditFeedbackService
{
    public MockRedditFeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching mock Reddit data...");
        await Task.Delay(1000); // Simulate network delay

        var mockThreads = new List<RedditThreadModel>
        {
            new()
            {
                Id = "1k1avda",
                Title = "Our ASP.NET Web Site is more performant than our .NET Core app. Why?",
                Author = "zerquet",
                SelfText = "Hello everyone, I have an ASP.NET Web Site (yes web forms and .net framework 4.x) that just has 3 pages showing users their compliance, so lots of database calls...",
                Url = "https://www.reddit.com/r/dotnet/comments/1k1avda/our_aspnet_web_site_is_more_performant_than_our/",
                Subreddit = "dotnet",
                Score = 22,
                UpvoteRatio = 0.67,
                NumComments = 3,
                Comments = new List<RedditCommentModel>
                {
                    new()
                    {
                        Id = "c1",
                        ParentId = "1k1avda",
                        Author = "phuber",
                        Body = "You are going to have to run a profiler and do performance analysis. No one here wrote your code and it could be a multitude of issues.",
                        Score = 98,
                        Replies = new List<RedditCommentModel>
                        {
                            new()
                            {
                                Id = "c2",
                                ParentId = "c1",
                                Author = "zerquet",
                                Body = "Thanks, will try that. Any recommended profiling tools?",
                                Score = 5,
                                Replies = null!
                            }
                        }
                    },
                    new()
                    {
                        Id = "c3",
                        ParentId = "1k1avda",
                        Author = "wllmsaccnt",
                        Body = "ASP.NET Core pulls its thread from the thread pool. You might want to review that you are not doing any blocking IO...",
                        Score = 32,
                        Replies = null!
                    }
                }
            }
        };

        // Build the comments string
        var allComments = string.Join("\n\n", mockThreads.Select(t =>
        {
            var threadComments = new List<string>();
            void AddComment(RedditCommentModel comment, int depth = 0)
            {
                var indent = new string(' ', depth * 2);
                threadComments.Add($"{indent}Comment by {comment.Author}: {comment.Body}");
                
                if (comment.Replies?.Any() == true)
                {
                    foreach (var reply in comment.Replies)
                    {
                        AddComment(reply, depth + 1);
                    }
                }
            }

            threadComments.Add($"Thread: {t.Title}\nAuthor: {t.Author}\nContent: {t.SelfText}");
            foreach (var comment in t.Comments)
            {
                AddComment(comment);
            }

            return string.Join("\n", threadComments);
        }));

        // Count total comments including nested replies
        int totalComments = mockThreads.Sum(t =>
        {
            int count = 0;
            void CountComments(RedditCommentModel comment)
            {
                count++;
                if (comment.Replies?.Any() == true)
                {
                    foreach (var reply in comment.Replies)
                    {
                        CountComments(reply);
                    }
                }
            }
            foreach (var comment in t.Comments)
            {
                CountComments(comment);
            }
            return count;
        });

        return (allComments, totalComments, mockThreads);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock Reddit comments...");
        await Task.Delay(1000); // Simulate analysis delay

        // Use provided comment count or calculate from threads if available
        int totalComments = commentCount ?? (additionalData as List<RedditThreadModel>)?.Sum(t => t.NumComments) ?? 3;

        var mockAnalysis = @$"# Reddit Discussion Analysis 👽

## Overall Sentiment & Engagement
🎯 Active technical discussion with high-quality responses ({totalComments} total comments)
📊 Main post has moderate engagement (67% upvote ratio)
💬 Key responses highly upvoted (98 and 32 points)

## Key Technical Points
1. Performance issues between ASP.NET and .NET Core applications
2. Importance of proper profiling and analysis
3. Discussion of thread pool management in ASP.NET Core

## Recommendations
1. Use profiling tools to identify bottlenecks
2. Check for blocking I/O operations
3. Review thread pool configuration
4. Consider async/await implementation

## Community Interaction
- High engagement from {totalComments} developers
- Constructive discussion format
- Good follow-up questions from OP

## Action Items
1. Implement profiling
2. Review async operations
3. Analyze thread pool usage";

        return (mockAnalysis, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments with count
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}