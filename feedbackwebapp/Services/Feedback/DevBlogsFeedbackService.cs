using SharedDump.Models.DevBlogs;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public class DevBlogsFeedbackService : FeedbackService, IDevBlogsFeedbackService
{
    public string ArticleUrl { get; set; } = string.Empty;

    public DevBlogsFeedbackService(IHttpClientFactory http, IConfiguration configuration, UserSettingsService userSettings, IAuthenticationHeaderService authHeaderService, string articleUrl, FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, authHeaderService, onStatusUpdate)
    {
        ArticleUrl = articleUrl;
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null)
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching DevBlogs comments...");
        if (string.IsNullOrWhiteSpace(ArticleUrl))
            throw new InvalidOperationException("DevBlogs article URL is required.");

        var devBlogsCode = Configuration["FeedbackApi:FunctionsKey"] ?? throw new InvalidOperationException("DevBlogs feedback API code is required in configuration (FeedbackApi:FunctionsKey)");
        var url = $"{BaseUrl}/api/GetDevBlogsFeedback?articleUrl={Uri.EscapeDataString(ArticleUrl)}&code={Uri.EscapeDataString(devBlogsCode)}";
        var response = await SendAuthenticatedRequestAsync(HttpMethod.Get, url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to fetch DevBlogs comments: {error}");
        }
        var responseContent = await response.Content.ReadAsStringAsync();
        
        var article = JsonSerializer.Deserialize<DevBlogsArticleModel>(responseContent);
        var totalComments = article?.Comments != null ? CountComments(article.Comments) : 0;
        
        return (responseContent, totalComments, article);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        var article = JsonSerializer.Deserialize<DevBlogsArticleModel>(comments);
        if (article == null || article.Comments == null || article.Comments.Count == 0)
        {
            UpdateStatus(FeedbackProcessStatus.Completed, "No comments to analyze");
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        var totalComments = commentCount ?? CountComments(article.Comments);
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, $"Analyzing {totalComments} comments...");

        // Analyze the comments and return both the markdown result and the comments list
        var markdown = await AnalyzeCommentsInternal("devblogs", comments, totalComments);
        return (markdown, article);
    }

    private int CountComments(List<DevBlogsCommentModel> comments)
    {
        return comments.Sum(c => 1 + (c.Replies?.Count > 0 ? CountComments(c.Replies) : 0));
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", additionalData);
        }

        // Analyze comments
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
