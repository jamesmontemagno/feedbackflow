using SharedDump.Models.Reddit;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

public class RedditServiceAdapter : IRedditService
{
    private readonly RedditService _redditService;

    public RedditServiceAdapter(RedditService redditService)
    {
        _redditService = redditService ?? throw new ArgumentNullException(nameof(redditService));
    }

    // Original service methods for compatibility
    public async Task<RedditThreadModel> GetThreadWithComments(string threadId)
    {
        return await _redditService.GetThreadWithComments(threadId);
    }

    public async Task<List<RedditThreadModel>> GetSubredditThreadsBasicInfo(string subreddit, string sortBy = "hot", DateTimeOffset? cutoffDate = null)
    {
        return await _redditService.GetSubredditThreadsBasicInfo(subreddit, sortBy, cutoffDate);
    }

    // New unified interface methods
    public async Task<RedditListing<RedditSubmission>> GetSubredditPostsAsync(string subreddit, string sort = "hot", int limit = 25)
    {
        var threads = await _redditService.GetSubredditThreadsBasicInfo(subreddit, sort);
        
        var submissions = threads.Take(limit).Select(t => new RedditSubmission
        {
            Id = t.Id,
            Title = t.Title,
            SelfText = t.SelfText ?? string.Empty,
            Author = t.Author,
            Subreddit = subreddit,
            SubredditNamePrefixed = $"r/{subreddit}",
            Created = t.CreatedUtc.ToUnixTimeSeconds(),
            CreatedUtc = t.CreatedUtc.ToUnixTimeSeconds(),
            Score = t.Score,
            UpvoteRatio = (float)t.UpvoteRatio,
            NumComments = t.NumComments,
            Url = t.Url ?? string.Empty,
            Permalink = t.Permalink ?? string.Empty,
            IsSelf = string.IsNullOrEmpty(t.Url),
            Over18 = false,
            PostHint = string.IsNullOrEmpty(t.Url) ? "self" : "link"
        }).ToList();

        return new RedditListing<RedditSubmission>
        {
            Data = new RedditListingData<RedditSubmission>
            {
                Children = submissions.Select(s => new RedditThing<RedditSubmission>
                {
                    Kind = "t3",
                    Data = s
                }).ToList(),
                After = submissions.Count >= limit ? "next_page_token" : null,
                Before = null
            }
        };
    }

    public async Task<RedditSubmission?> GetSubmissionAsync(string submissionId)
    {
        var thread = await _redditService.GetThreadWithComments(submissionId);
        if (thread == null) return null;

        return new RedditSubmission
        {
            Id = thread.Id,
            Title = thread.Title,
            SelfText = thread.SelfText ?? string.Empty,
            Author = thread.Author,
            Subreddit = thread.Subreddit,
            SubredditNamePrefixed = $"r/{thread.Subreddit}",
            Created = thread.CreatedUtc.ToUnixTimeSeconds(),
            CreatedUtc = thread.CreatedUtc.ToUnixTimeSeconds(),
            Score = thread.Score,
            UpvoteRatio = (float)thread.UpvoteRatio,
            NumComments = thread.NumComments,
            Url = thread.Url ?? string.Empty,
            Permalink = thread.Permalink ?? string.Empty,
            IsSelf = string.IsNullOrEmpty(thread.Url),
            Over18 = false,
            PostHint = string.IsNullOrEmpty(thread.Url) ? "self" : "link"
        };
    }

    public async Task<RedditListing<RedditComment>> GetSubmissionCommentsAsync(string submissionId, string sort = "top", int limit = 100)
    {
        var thread = await _redditService.GetThreadWithComments(submissionId);
        if (thread?.Comments == null) 
        {
            return new RedditListing<RedditComment>
            {
                Data = new RedditListingData<RedditComment>
                {
                    Children = new List<RedditThing<RedditComment>>(),
                    After = null,
                    Before = null
                }
            };
        }

        var comments = thread.Comments.Take(limit).Select(c => new RedditComment
        {
            Id = c.Id,
            Author = c.Author,
            Body = c.Body,
            Created = c.CreatedUtc.ToUnixTimeSeconds(),
            CreatedUtc = c.CreatedUtc.ToUnixTimeSeconds(),
            Score = c.Score,
            ParentId = c.ParentId ?? $"t3_{submissionId}",
            LinkId = $"t3_{submissionId}",
            Subreddit = thread.Subreddit,
            SubredditNamePrefixed = $"r/{thread.Subreddit}",
            Permalink = $"/r/{thread.Subreddit}/comments/{submissionId}//comment_{c.Id}/"
        }).ToList();

        return new RedditListing<RedditComment>
        {
            Data = new RedditListingData<RedditComment>
            {
                Children = comments.Select(c => new RedditThing<RedditComment>
                {
                    Kind = "t1",
                    Data = c
                }).ToList(),
                After = null,
                Before = null
            }
        };
    }

    public async Task<RedditListing<RedditSubmission>> SearchPostsAsync(string query, string subreddit = "", string sort = "relevance", int limit = 25)
    {
        var threads = await _redditService.SearchPosts(query, subreddit, sort, limit);
        
        var submissions = threads.Select(t => new RedditSubmission
        {
            Id = t.Id,
            Title = t.Title,
            SelfText = t.SelfText ?? string.Empty,
            Author = t.Author,
            Subreddit = string.IsNullOrEmpty(t.Subreddit) ? subreddit : t.Subreddit,
            SubredditNamePrefixed = string.IsNullOrEmpty(t.Subreddit) ? $"r/{subreddit}" : $"r/{t.Subreddit}",
            Created = t.CreatedUtc.ToUnixTimeSeconds(),
            CreatedUtc = t.CreatedUtc.ToUnixTimeSeconds(),
            Score = t.Score,
            UpvoteRatio = (float)t.UpvoteRatio,
            NumComments = t.NumComments,
            Url = t.Url ?? string.Empty,
            Permalink = t.Permalink ?? string.Empty,
            IsSelf = string.IsNullOrEmpty(t.Url),
            Over18 = false,
            PostHint = string.IsNullOrEmpty(t.Url) ? "self" : "link"
        }).ToList();

        return new RedditListing<RedditSubmission>
        {
            Data = new RedditListingData<RedditSubmission>
            {
                Children = submissions.Select(s => new RedditThing<RedditSubmission>
                {
                    Kind = "t3",
                    Data = s
                }).ToList(),
                After = submissions.Count >= limit ? "next_page_token" : null,
                Before = null
            }
        };
    }

    // Validation method
    public async Task<bool> CheckSubredditValid(string subreddit)
    {
        try
        {
            // Try to get basic info about the subreddit
            var posts = await GetSubredditPostsAsync(subreddit, "hot", 1);
            return posts?.Data?.Children != null;
        }
        catch
        {
            // If any exception occurs (like subreddit not found, private, banned, etc.), 
            // consider it invalid
            return false;
        }
    }

    public async Task<RedditSubredditInfo> GetSubredditInfo(string subreddit)
    {
        return await _redditService.GetSubredditInfo(subreddit);
    }
}
