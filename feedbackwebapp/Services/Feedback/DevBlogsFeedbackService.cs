using System.Net.Http.Json;
using SharedDump.Models.DevBlogs;
using FeedbackWebApp.Services.Interfaces;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public class DevBlogsFeedbackService : FeedbackService, IDevBlogsFeedbackService
{
    public string ArticleUrl { get; set; } = string.Empty;

    public DevBlogsFeedbackService(HttpClient http, IConfiguration configuration, UserSettingsService userSettings, string articleUrl, FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
        ArticleUrl = articleUrl;
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching DevBlogs comments...");
        if (string.IsNullOrWhiteSpace(ArticleUrl))
            throw new InvalidOperationException("DevBlogs article URL is required.");

        var devBlogsCode = Configuration["FeedbackApi:GetDevBlogsFeedbackCode"] ?? throw new InvalidOperationException("DevBlogs feedback API code is required in configuration (FeedbackApi:GetDevBlogsFeedbackCode)");
        var url = $"{BaseUrl}/api/GetDevBlogsFeedback?articleUrl={Uri.EscapeDataString(ArticleUrl)}&code={Uri.EscapeDataString(devBlogsCode)}";
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to fetch DevBlogs comments: {error}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var article = JsonSerializer.Deserialize<DevBlogsArticleModel>(responseContent);

        if (article == null)
            throw new InvalidOperationException("No comments found or failed to parse article.");        // Count total comments including replies recursively
        int CountComments(List<DevBlogsCommentModel> comments)
        {
            return comments.Sum(c => 1 + (c.Replies?.Count > 0 ? CountComments(c.Replies) : 0));
        }

        var totalComments = CountComments(article.Comments);
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing comments...");

        // Analyze the comments and return both the markdown result and the comments list
        var markdown = await AnalyzeComments("devblogs", responseContent, totalComments);
        return (markdown, article);
    }
}
