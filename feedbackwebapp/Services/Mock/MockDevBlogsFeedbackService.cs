using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models.DevBlogs;

namespace FeedbackWebApp.Services.Mock;

public class MockDevBlogsFeedbackService : FeedbackService, IDevBlogsFeedbackService
{
    public string ArticleUrl { get; set; } = string.Empty;

    public MockDevBlogsFeedbackService(IHttpClientFactory http, IConfiguration configuration, UserSettingsService userSettings, FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate) { }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        await Task.Delay(500); // Simulate network delay
        var mockArticle = new DevBlogsArticleModel
        {
            Title = "Mock DevBlogs Article",
            Url = ArticleUrl,
            Comments = new List<DevBlogsCommentModel>
            {
                new() {
                    Id = "1",
                    Author = "Alice",
                    BodyHtml = "Great post!",
                    PublishedUtc = DateTimeOffset.UtcNow,
                    Replies = new List<DevBlogsCommentModel> {
                        new() {
                            Id = "1.1",
                            Author = "Bob",
                            BodyHtml = "Thanks Alice! I agree.",
                            PublishedUtc = DateTimeOffset.UtcNow,
                            Replies = new List<DevBlogsCommentModel> {
                                new() {
                                    Id = "1.1.1",
                                    Author = "Carol",
                                    BodyHtml = "Can you elaborate more?",
                                    PublishedUtc = DateTimeOffset.UtcNow
                                }
                            }
                        },
                        new() {
                            Id = "1.2",
                            Author = "Dan",
                            BodyHtml = "I have a different opinion.",
                            PublishedUtc = DateTimeOffset.UtcNow
                        }
                    }
                },
                new() {
                    Id = "2",
                    Author = "Eve",
                    BodyHtml = "I have a question...",
                    PublishedUtc = DateTimeOffset.UtcNow
                }
            }
        };
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching DevBlogs feedback...");
        await Task.Delay(1000); // Simulate analysis time
        
        var mockMarkdown = @"## DevBlogs Feedback Analysis

    ### Overview
    Analysis of comments from the article 'Mock DevBlogs Article'
    Total comments analyzed: 5 (including replies)

    ### Key Points
    - 👍 Positive sentiment observed in initial responses
    - ❓ Several questions raised by readers
    - 💭 Notable discussion thread with multiple perspectives

    ### Detailed Breakdown

    #### Main Themes
    1. General Appreciation
       - Users expressed positive feedback
       - Engagement levels appear healthy

    2. Discussion Points
       - Request for additional clarification
       - Some contrasting viewpoints presented

    #### Recommendations
    - Consider addressing Carol's request for elaboration
    - Follow up on Eve's question for better engagement
    - Monitor the discussion thread for further insights

    ### Sentiment Distribution
    - Positive: 60%
    - Neutral: 30%
    - Critical: 10%";

        return (mockMarkdown, mockArticle);
    }
}
