using SharedDump.Models.DevBlogs;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

public class DevBlogsServiceAdapter : IDevBlogsService
{
    private readonly DevBlogsService _devBlogsService;

    public DevBlogsServiceAdapter(DevBlogsService devBlogsService)
    {
        _devBlogsService = devBlogsService ?? throw new ArgumentNullException(nameof(devBlogsService));
    }

    // Original service method for compatibility
    public async Task<DevBlogsArticleModel?> FetchArticleWithCommentsAsync(string articleUrl)
    {
        return await _devBlogsService.FetchArticleWithCommentsAsync(articleUrl);
    }

    // New unified interface methods
    public Task<List<DevBlogsArticle>> GetLatestArticlesAsync(int count = 10)
    {
        // The original service focuses on individual articles with comments
        // For this adapter, we'll return a placeholder implementation
        // In a real scenario, you'd implement RSS feed parsing for article listings
        return Task.FromResult(new List<DevBlogsArticle>());
    }

    public Task<List<DevBlogsArticle>> GetArticlesByCategoryAsync(string category, int count = 10)
    {
        // Original service doesn't support category filtering
        return Task.FromResult(new List<DevBlogsArticle>());
    }

    public Task<List<DevBlogsArticle>> SearchArticlesAsync(string query, int count = 10)
    {
        // Original service doesn't support search
        return Task.FromResult(new List<DevBlogsArticle>());
    }

    public Task<DevBlogsArticle?> GetArticleByGuidAsync(string guid)
    {
        // The original service works with URLs, not GUIDs
        // This would need URL-to-GUID mapping in a real implementation
        return Task.FromResult<DevBlogsArticle?>(null);
    }

    public Task<List<string>> GetAvailableCategoriesAsync()
    {
        // Return some common DevBlogs categories
        return Task.FromResult(new List<string>
        {
            ".NET", "Azure", "Visual Studio", "ASP.NET", "C#", 
            "Performance", "AI", "Cloud", "Developer Tools"
        });
    }

    public Task<List<DevBlogsArticle>> GetArticlesByAuthorAsync(string author, int count = 10)
    {
        // Original service doesn't support author filtering
        return Task.FromResult(new List<DevBlogsArticle>());
    }

    public Task<List<DevBlogsArticle>> GetArticlesByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, int count = 10)
    {
        // Original service doesn't support date range filtering
        return Task.FromResult(new List<DevBlogsArticle>());
    }
}
