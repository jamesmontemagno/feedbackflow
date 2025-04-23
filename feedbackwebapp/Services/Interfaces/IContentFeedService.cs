using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Services.Interfaces;

public interface IContentFeedService
{
    Task<object?> FetchContent();
}

public interface IYouTubeContentFeedService : IContentFeedService 
{
    new Task<List<YouTubeOutputVideo>> FetchContent();
}

public interface IRedditContentFeedService : IContentFeedService 
{
    new Task<List<RedditThreadModel>> FetchContent();
}

public interface IHackerNewsContentFeedService : IContentFeedService 
{
    new Task<List<HackerNewsItem>> FetchContent();
}