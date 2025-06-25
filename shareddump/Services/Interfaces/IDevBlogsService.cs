using SharedDump.Models.DevBlogs;

namespace SharedDump.Services.Interfaces;

public interface IDevBlogsService
{
    // Original service method for compatibility
    Task<DevBlogsArticleModel?> FetchArticleWithCommentsAsync(string articleUrl);
    
    // New unified interface methods
    Task<List<DevBlogsArticle>> GetLatestArticlesAsync(int count = 10);
    Task<List<DevBlogsArticle>> GetArticlesByCategoryAsync(string category, int count = 10);
    Task<List<DevBlogsArticle>> SearchArticlesAsync(string query, int count = 10);
    Task<DevBlogsArticle?> GetArticleByGuidAsync(string guid);
    Task<List<string>> GetAvailableCategoriesAsync();
    Task<List<DevBlogsArticle>> GetArticlesByAuthorAsync(string author, int count = 10);
    Task<List<DevBlogsArticle>> GetArticlesByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, int count = 10);
}
