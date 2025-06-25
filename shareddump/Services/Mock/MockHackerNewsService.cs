using SharedDump.Models.HackerNews;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

public class MockHackerNewsService : IHackerNewsService
{
    private readonly List<HackerNewsStory> _mockStories;
    private readonly List<HackerNewsComment> _mockComments;

    public MockHackerNewsService()
    {
        // Use shared mock data provider
        _mockStories = MockDataProvider.HackerNews.GetMockStories();
        _mockComments = MockDataProvider.HackerNews.GetMockComments();
    }

    // Original service methods for compatibility
    public async Task<HackerNewsItem?> GetItemData(int itemId)
    {
        await Task.Delay(50); // Simulate API delay
        
        var story = _mockStories.FirstOrDefault(s => s.Id == itemId);
        if (story != null)
        {
            return new HackerNewsItem
            {
                Id = story.Id,
                Title = story.Title,
                Url = story.Url,
                Score = story.Score,
                By = story.By,
                Time = story.Time,
                Descendants = story.Descendants,
                Type = story.Type,
                Kids = story.Kids?.ToList() ?? new List<int>()
            };
        }

        var comment = _mockComments.FirstOrDefault(c => c.Id == itemId);
        if (comment != null)
        {
            return new HackerNewsItem
            {
                Id = comment.Id,
                By = comment.By,
                Parent = comment.Parent,
                Text = comment.Text,
                Time = comment.Time,
                Type = comment.Type,
                Kids = comment.Kids?.ToList() ?? new List<int>()
            };
        }

        return null;
    }

    public async IAsyncEnumerable<HackerNewsItem> GetItemWithComments(int itemId)
    {
        await Task.Delay(100); // Simulate API delay
        
        var story = await GetItemData(itemId);
        if (story != null)
        {
            story.MainStoryId = itemId;
            yield return story;
            
            // Return all comments for this story
            var comments = await GetCommentsForStoryAsync(itemId);
            foreach (var comment in comments)
            {
                yield return new HackerNewsItem
                {
                    Id = comment.Id,
                    By = comment.By,
                    Parent = comment.Parent,
                    Text = comment.Text,
                    Time = comment.Time,
                    Type = comment.Type,
                    Kids = comment.Kids?.ToList() ?? new List<int>(),
                    MainStoryId = itemId
                };
            }
        }
    }

    public async Task<int[]> GetTopStories()
    {
        await Task.Delay(200); // Simulate API delay
        return _mockStories.OrderByDescending(s => s.Score).Select(s => s.Id).ToArray();
    }

    public async Task<List<HackerNewsItemBasicInfo>> SearchByTitleBasicInfo()
    {
        await Task.Delay(300); // Simulate API delay
        return _mockStories.Select(s => new HackerNewsItemBasicInfo
        {
            Id = s.Id,
            Title = s.Title,
            By = s.By,
            Time = s.Time,
            Url = s.Url,
            Score = s.Score,
            Descendants = s.Descendants
        }).ToList();
    }

    // New unified interface methods
    public async Task<HackerNewsStory?> GetStoryAsync(int itemId)
    {
        await Task.Delay(100); // Simulate API delay
        return _mockStories.FirstOrDefault(s => s.Id == itemId);
    }

    public async Task<HackerNewsComment?> GetCommentAsync(int itemId)
    {
        await Task.Delay(50); // Simulate API delay
        return _mockComments.FirstOrDefault(c => c.Id == itemId);
    }

    public async Task<int[]> GetTopStoriesAsync()
    {
        await Task.Delay(200); // Simulate API delay
        return _mockStories.OrderByDescending(s => s.Score).Select(s => s.Id).ToArray();
    }

    public async Task<int[]> GetNewStoriesAsync()
    {
        await Task.Delay(200); // Simulate API delay
        return _mockStories.OrderByDescending(s => s.Time).Select(s => s.Id).ToArray();
    }

    public async Task<List<HackerNewsComment>> GetCommentsForStoryAsync(int storyId)
    {
        await Task.Delay(150); // Simulate API delay
        
        var story = await GetStoryAsync(storyId);
        if (story?.Kids == null) return new List<HackerNewsComment>();

        var comments = new List<HackerNewsComment>();
        foreach (var kidId in story.Kids)
        {
            var comment = await GetCommentAsync(kidId);
            if (comment != null)
            {
                comments.Add(comment);
                
                // Add nested comments if they exist
                if (comment.Kids != null)
                {
                    foreach (var nestedKidId in comment.Kids)
                    {
                        var nestedComment = await GetCommentAsync(nestedKidId);
                        if (nestedComment != null)
                        {
                            comments.Add(nestedComment);
                        }
                    }
                }
            }
        }

        return comments.OrderBy(c => c.Time).ToList();
    }
}
