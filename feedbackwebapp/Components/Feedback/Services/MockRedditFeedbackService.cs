using SharedDump.Models.Reddit;
using FeedbackWebApp.Services;

namespace FeedbackWebApp.Components.Feedback.Services;

public class MockRedditFeedbackService : FeedbackService, IRedditFeedbackService
{
    public MockRedditFeedbackService(
        HttpClient http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null) 
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
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

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing mock Reddit comments...");
        await Task.Delay(1000); // Simulate analysis delay

        var mockAnalysis = @"# Reddit Discussion Analysis 👽

## Overall Sentiment & Engagement
🎯 Active technical discussion with high-quality responses
📊 Main post has moderate engagement (67% upvote ratio)
💬 Key responses highly upvoted (98 and 32 points)

## Key Technical Points
🔍 Performance profiling recommended as first step
⚡ Thread pool and async IO considerations in .NET Core
🛠️ Potential blocking IO issues identified

## Notable Insights
- Detailed explanation of ASP.NET Core threading model
- Importance of proper performance testing
- Community focus on systematic debugging approach

## Community Response
👥 Experienced developers providing detailed technical guidance
📈 Strong emphasis on proper diagnostic approaches
🤝 Constructive discussion tone with practical advice";

        return (mockAnalysis, mockThreads);
    }
}