using SharedDump.Models.DevBlogs;

namespace FeedbackWebApp.Services.Interfaces;

public interface IDevBlogsFeedbackService : IFeedbackService
{
    /// <summary>
    /// Fetches and analyzes DevBlogs article comments.
    /// </summary>
    /// <returns>Markdown result and the parsed article model as additionalData.</returns>
    new Task<(string markdownResult, object? additionalData)> GetFeedback();
    string ArticleUrl { get; set; }
}
