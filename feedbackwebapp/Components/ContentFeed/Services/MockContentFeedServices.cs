using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Components.ContentFeed.Services;

public class MockYouTubeContentFeedService : ContentFeedService, IYouTubeContentFeedService
{
    public MockYouTubeContentFeedService(HttpClient http, IConfiguration configuration)
        : base(http, configuration) { }

    public override async Task<object?> FetchContent()
    {
        return await ((IYouTubeContentFeedService)this).FetchContent();
    }

    async Task<List<YouTubeOutputVideo>> IYouTubeContentFeedService.FetchContent()
    {
        await Task.Delay(500); // Simulate network delay
        return new List<YouTubeOutputVideo>
        {
            new() { Id = "mock1", Title = "Mock YouTube Video 1" },
            new() { Id = "mock2", Title = "Mock YouTube Video 2" }
        };
    }
}

public class MockRedditContentFeedService : ContentFeedService, IRedditContentFeedService
{
    public MockRedditContentFeedService(HttpClient http, IConfiguration configuration)
        : base(http, configuration) { }

    public override async Task<object?> FetchContent()
    {
        return await ((IRedditContentFeedService)this).FetchContent();
    }

    async Task<List<RedditThreadModel>> IRedditContentFeedService.FetchContent()
    {
        await Task.Delay(500); // Simulate network delay
        return new List<RedditThreadModel>
        {
            new() { Id = "mock1", Title = "Mock Reddit Thread 1", Author = "user1" },
            new() { Id = "mock2", Title = "Mock Reddit Thread 2", Author = "user2" }
        };
    }
}

public class MockHackerNewsContentFeedService : ContentFeedService, IHackerNewsContentFeedService
{
    public MockHackerNewsContentFeedService(HttpClient http, IConfiguration configuration)
        : base(http, configuration) { }

    public override async Task<object?> FetchContent()
    {
        return await ((IHackerNewsContentFeedService)this).FetchContent();
    }

    async Task<List<HackerNewsItem>> IHackerNewsContentFeedService.FetchContent()
    {
        await Task.Delay(500); // Simulate network delay
        return new List<HackerNewsItem>
        {
            new() { Id = 1, Title = "Mock HN Story 1", By = "user1" },
            new() { Id = 2, Title = "Mock HN Story 2", By = "user2" }
        };
    }
}