using SharedDump.Models.Reddit;

namespace SharedDump.Services.Interfaces;

public interface IRedditService
{
    // Original service methods for compatibility
    Task<RedditThreadModel> GetThreadWithComments(string threadId);
    Task<List<RedditThreadModel>> GetSubredditThreadsBasicInfo(string subreddit, string sortBy = "hot", DateTimeOffset? cutoffDate = null);
    
    // New unified interface methods
    Task<RedditListing<RedditSubmission>> GetSubredditPostsAsync(string subreddit, string sort = "hot", int limit = 25);
    Task<RedditSubmission?> GetSubmissionAsync(string submissionId);
    Task<RedditListing<RedditComment>> GetSubmissionCommentsAsync(string submissionId, string sort = "top", int limit = 100);
    Task<RedditListing<RedditSubmission>> SearchPostsAsync(string query, string subreddit = "", string sort = "relevance", int limit = 25);
    
    // Subreddit information
    Task<RedditSubredditInfo> GetSubredditInfo(string subreddit);
    
    // Validation method
    Task<bool> CheckSubredditValid(string subreddit);
}
