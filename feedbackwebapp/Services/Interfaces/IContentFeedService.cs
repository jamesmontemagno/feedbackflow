using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.HackerNews;

namespace FeedbackWebApp.Services.Interfaces;

/// <summary>
/// Provides services for fetching content from various sources
/// </summary>
/// <remarks>
/// This base interface allows for retrieving content from any supported source.
/// Implement this interface when adding a new content source to the application.
/// </remarks>
/// <example>
/// <code>
/// public class MyContentService : IContentFeedService
/// {
///     public async Task&lt;object?&gt; FetchContent()
///     {
///         // Fetch content from source
///         return await GetDataFromSource();
///     }
/// }
/// </code>
/// </example>
public interface IContentFeedService
{
    /// <summary>
    /// Fetches content from the source
    /// </summary>
    /// <returns>The content as an object that must be cast to the appropriate type</returns>
    Task<object?> FetchContent();
}

/// <summary>
/// YouTube-specific content feed service
/// </summary>
/// <remarks>
/// Used to retrieve videos and related data from YouTube
/// </remarks>
public interface IYouTubeContentFeedService : IContentFeedService 
{
    /// <summary>
    /// Fetches YouTube videos and their comments
    /// </summary>
    /// <returns>A list of YouTube video data including comments</returns>
    new Task<List<YouTubeOutputVideo>> FetchContent();
}

/// <summary>
/// Reddit-specific content feed service
/// </summary>
/// <remarks>
/// Used to retrieve threads, posts, and comments from Reddit
/// </remarks>
public interface IRedditContentFeedService : IContentFeedService 
{
    /// <summary>
    /// Fetches Reddit threads with their comments
    /// </summary>
    /// <returns>A list of Reddit threads with comment data</returns>
    new Task<List<RedditThreadModel>> FetchContent();
}

/// <summary>
/// Hacker News-specific content feed service
/// </summary>
/// <remarks>
/// Used to retrieve stories and comments from Hacker News
/// </remarks>
public interface IHackerNewsContentFeedService : IContentFeedService 
{
    /// <summary>
    /// Fetches Hacker News stories and comments
    /// </summary>
    /// <returns>A list of Hacker News items including stories and comments</returns>
    new Task<List<HackerNewsItem>> FetchContent();
}