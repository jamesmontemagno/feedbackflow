using SharedDump.Models.HackerNews;

namespace SharedDump.Services.Interfaces;

public interface IHackerNewsService
{
    // Original service methods for compatibility
    Task<HackerNewsItem?> GetItemData(int itemId);
    IAsyncEnumerable<HackerNewsItem> GetItemWithComments(int itemId);
    Task<int[]> GetTopStories();
    Task<List<HackerNewsItemBasicInfo>> SearchByTitleBasicInfo();
    
    // New unified interface methods
    Task<HackerNewsStory?> GetStoryAsync(int itemId);
    Task<HackerNewsComment?> GetCommentAsync(int itemId);
    Task<int[]> GetTopStoriesAsync();
    Task<int[]> GetNewStoriesAsync();
    Task<List<HackerNewsComment>> GetCommentsForStoryAsync(int storyId);
}
