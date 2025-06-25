using SharedDump.Models.HackerNews;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

public class HackerNewsServiceAdapter : IHackerNewsService
{
    private readonly HackerNewsService _hackerNewsService;

    public HackerNewsServiceAdapter(HackerNewsService hackerNewsService)
    {
        _hackerNewsService = hackerNewsService ?? throw new ArgumentNullException(nameof(hackerNewsService));
    }

    // Original service methods for compatibility
    public async Task<HackerNewsItem?> GetItemData(int itemId)
    {
        return await _hackerNewsService.GetItemData(itemId);
    }

    public IAsyncEnumerable<HackerNewsItem> GetItemWithComments(int itemId)
    {
        return _hackerNewsService.GetItemWithComments(itemId);
    }

    public async Task<int[]> GetTopStories()
    {
        return await _hackerNewsService.GetTopStories();
    }

    public async Task<List<HackerNewsItemBasicInfo>> SearchByTitleBasicInfo()
    {
        return await _hackerNewsService.SearchByTitleBasicInfo();
    }

    // New unified interface methods
    public async Task<HackerNewsStory?> GetStoryAsync(int itemId)
    {
        var item = await _hackerNewsService.GetItemData(itemId);
        if (item == null || item.Type != "story") return null;

        return new HackerNewsStory
        {
            Id = item.Id,
            Title = item.Title ?? string.Empty,
            Url = item.Url,
            Score = item.Score ?? 0,
            By = item.By ?? string.Empty,
            Time = item.Time,
            Descendants = item.Descendants ?? 0,
            Type = item.Type ?? "story",
            Kids = item.Kids?.ToArray()
        };
    }

    public async Task<HackerNewsComment?> GetCommentAsync(int itemId)
    {
        var item = await _hackerNewsService.GetItemData(itemId);
        if (item == null || item.Type != "comment") return null;

        return new HackerNewsComment
        {
            Id = item.Id,
            By = item.By ?? string.Empty,
            Parent = item.Parent ?? 0,
            Text = item.Text ?? string.Empty,
            Time = item.Time,
            Type = item.Type ?? "comment",
            Kids = item.Kids?.ToArray()
        };
    }

    public async Task<int[]> GetTopStoriesAsync()
    {
        return await _hackerNewsService.GetTopStories();
    }

    public async Task<int[]> GetNewStoriesAsync()
    {
        // The original service doesn't have GetNewStories, so we'll use GetTopStories as a fallback
        // In a real implementation, you'd call the appropriate endpoint
        return await _hackerNewsService.GetTopStories();
    }

    public async Task<List<HackerNewsComment>> GetCommentsForStoryAsync(int storyId)
    {
        var comments = new List<HackerNewsComment>();
        
        await foreach (var item in _hackerNewsService.GetItemWithComments(storyId))
        {
            if (item.Type == "comment")
            {
                comments.Add(new HackerNewsComment
                {
                    Id = item.Id,
                    By = item.By ?? string.Empty,
                    Parent = item.Parent ?? 0,
                    Text = item.Text ?? string.Empty,
                    Time = item.Time,
                    Type = item.Type ?? "comment",
                    Kids = item.Kids?.ToArray()
                });
            }
        }

        return comments;
    }
}
