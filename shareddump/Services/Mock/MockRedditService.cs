using SharedDump.Models.Reddit;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

public class MockRedditService : IRedditService
{
    private readonly List<RedditSubmission> _mockSubmissions = new()
    {
        new RedditSubmission
        {
            Id = "abc123",
            Title = "What's the best way to handle async/await in C#?",
            SelfText = "I've been working with async/await patterns and I'm wondering about best practices for error handling and cancellation tokens.",
            Author = "async_learner",
            Subreddit = "dotnet",
            SubredditNamePrefixed = "r/dotnet",
            Created = DateTimeOffset.UtcNow.AddHours(-3).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3).ToUnixTimeSeconds(),
            Score = 127,
            UpvoteRatio = 0.92f,
            NumComments = 34,
            Url = "https://www.reddit.com/r/dotnet/comments/abc123/whats_the_best_way_to_handle_asyncawait_in_c/",
            Permalink = "/r/dotnet/comments/abc123/whats_the_best_way_to_handle_asyncawait_in_c/",
            IsSelf = true,
            Over18 = false,
            PostHint = "self"
        },
        new RedditSubmission
        {
            Id = "def456",
            Title = "Show Reddit: Built a GitHub integration for tracking project feedback",
            SelfText = "After struggling with scattered feedback across multiple platforms, I built a tool that aggregates GitHub issues, discussions, and PR comments into a unified dashboard. Would love to get your thoughts!",
            Author = "feedback_builder",
            Subreddit = "programming",
            SubredditNamePrefixed = "r/programming",
            Created = DateTimeOffset.UtcNow.AddHours(-5).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-5).ToUnixTimeSeconds(),
            Score = 89,
            UpvoteRatio = 0.88f,
            NumComments = 23,
            Url = "https://github.com/example/feedback-tool",
            Permalink = "/r/programming/comments/def456/show_reddit_built_a_github_integration_for/",
            IsSelf = false,
            Over18 = false,
            PostHint = "link"
        },
        new RedditSubmission
        {
            Id = "ghi789",
            Title = "YouTube API changes affecting content creators",
            SelfText = "The recent YouTube API changes have been impacting how we can access video analytics and comments. Has anyone found good workarounds?",
            Author = "content_creator_dev",
            Subreddit = "webdev",
            SubredditNamePrefixed = "r/webdev",
            Created = DateTimeOffset.UtcNow.AddHours(-8).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-8).ToUnixTimeSeconds(),
            Score = 156,
            UpvoteRatio = 0.94f,
            NumComments = 45,
            Url = "https://www.reddit.com/r/webdev/comments/ghi789/youtube_api_changes_affecting_content_creators/",
            Permalink = "/r/webdev/comments/ghi789/youtube_api_changes_affecting_content_creators/",
            IsSelf = true,
            Over18 = false,
            PostHint = "self"
        }
    };

    private readonly List<RedditComment> _mockComments = new()
    {
        new RedditComment
        {
            Id = "comment1",
            Author = "senior_dev",
            Body = "Great question! The key is to always use ConfigureAwait(false) in library code, and make sure you're handling OperationCanceledException properly. Here's a pattern I use:\n\n```csharp\ntry\n{\n    var result = await SomeAsyncOperation(cancellationToken).ConfigureAwait(false);\n    return result;\n}\ncatch (OperationCanceledException)\n{\n    // Handle cancellation appropriately\n    throw;\n}\n```",
            Created = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
            Score = 45,
            ParentId = "t3_abc123",
            LinkId = "t3_abc123",
            Subreddit = "dotnet",
            SubredditNamePrefixed = "r/dotnet",
            Permalink = "/r/dotnet/comments/abc123/whats_the_best_way_to_handle_asyncawait_in_c/comment1/"
        },
        new RedditComment
        {
            Id = "comment2",
            Author = "async_expert",
            Body = "Don't forget about ValueTask for hot paths! If you're doing a lot of async operations that might complete synchronously, ValueTask can reduce allocations significantly.",
            Created = DateTimeOffset.UtcNow.AddMinutes(-90).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-90).ToUnixTimeSeconds(),
            Score = 23,
            ParentId = "t1_comment1",
            LinkId = "t3_abc123",
            Subreddit = "dotnet",
            SubredditNamePrefixed = "r/dotnet",
            Permalink = "/r/dotnet/comments/abc123/whats_the_best_way_to_handle_asyncawait_in_c/comment2/"
        },
        new RedditComment
        {
            Id = "comment3",
            Author = "integration_enthusiast",
            Body = "This looks really useful! I've been manually checking GitHub notifications and it's become overwhelming. A few questions:\n\n1. Does it support GitHub Enterprise?\n2. Can you filter by labels or milestones?\n3. Any plans for Slack/Teams integration?",
            Created = DateTimeOffset.UtcNow.AddHours(-4).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-4).ToUnixTimeSeconds(),
            Score = 12,
            ParentId = "t3_def456",
            LinkId = "t3_def456",
            Subreddit = "programming",
            SubredditNamePrefixed = "r/programming",
            Permalink = "/r/programming/comments/def456/show_reddit_built_a_github_integration_for/comment3/"
        },
        new RedditComment
        {
            Id = "comment4",
            Author = "feedback_builder",
            Body = "Thanks for the interest! To answer your questions:\n\n1. GitHub Enterprise support is on the roadmap for Q2\n2. Yes! You can filter by labels, milestones, assignees, and more\n3. Slack integration is already available, Teams is coming next month\n\nFeel free to check out the demo at [link] or reach out if you want to be a beta tester!",
            Created = DateTimeOffset.UtcNow.AddHours(-3).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3).ToUnixTimeSeconds(),
            Score = 8,
            ParentId = "t1_comment3",
            LinkId = "t3_def456",
            Subreddit = "programming",
            SubredditNamePrefixed = "r/programming",
            Permalink = "/r/programming/comments/def456/show_reddit_built_a_github_integration_for/comment4/"
        },
        new RedditComment
        {
            Id = "comment5",
            Author = "api_ninja",
            Body = "I've been dealing with the same issues! The quota changes are particularly frustrating. I ended up implementing a caching layer with Redis to reduce API calls. Also, the new comment threading limits mean you have to be more strategic about which comments you fetch.",
            Created = DateTimeOffset.UtcNow.AddHours(-7).ToUnixTimeSeconds(),
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-7).ToUnixTimeSeconds(),
            Score = 34,
            ParentId = "t3_ghi789",
            LinkId = "t3_ghi789",
            Subreddit = "webdev",
            SubredditNamePrefixed = "r/webdev",
            Permalink = "/r/webdev/comments/ghi789/youtube_api_changes_affecting_content_creators/comment5/"
        }
    };

    // Original service methods for compatibility
    public async Task<RedditThreadModel> GetThreadWithComments(string threadId)
    {
        await Task.Delay(150); // Simulate API delay
        
        var submission = _mockSubmissions.FirstOrDefault(s => s.Id == threadId);
        if (submission == null)
        {
            throw new ArgumentException($"Thread with ID {threadId} not found");
        }

        var comments = _mockComments
            .Where(c => c.LinkId == $"t3_{threadId}")
            .Select(c => new RedditCommentModel
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Author = c.Author,
                Body = c.Body,
                Score = c.Score,
                CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(c.CreatedUtc),
                Replies = new List<RedditCommentModel>()
            }).ToList();

        return new RedditThreadModel
        {
            Id = submission.Id,
            Title = submission.Title,
            Author = submission.Author,
            SelfText = submission.SelfText,
            Url = submission.Url,
            Subreddit = submission.Subreddit,
            Score = submission.Score,
            UpvoteRatio = submission.UpvoteRatio,
            NumComments = submission.NumComments,
            Comments = comments,
            CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(submission.CreatedUtc),
            Permalink = submission.Permalink
        };
    }

    public async Task<List<RedditThreadModel>> GetSubredditThreadsBasicInfo(string subreddit, string sortBy = "hot", DateTimeOffset? cutoffDate = null)
    {
        await Task.Delay(200); // Simulate API delay
        
        var filteredSubmissions = _mockSubmissions
            .Where(s => s.Subreddit.Equals(subreddit, StringComparison.OrdinalIgnoreCase));

        if (cutoffDate.HasValue)
        {
            filteredSubmissions = filteredSubmissions
                .Where(s => DateTimeOffset.FromUnixTimeSeconds(s.CreatedUtc) >= cutoffDate.Value);
        }

        return filteredSubmissions.Select(s => new RedditThreadModel
        {
            Id = s.Id,
            Title = s.Title,
            Author = s.Author,
            SelfText = s.SelfText,
            Url = s.Url,
            Subreddit = s.Subreddit,
            Score = s.Score,
            UpvoteRatio = s.UpvoteRatio,
            NumComments = s.NumComments,
            Comments = new List<RedditCommentModel>(),
            CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(s.CreatedUtc),
            Permalink = s.Permalink
        }).ToList();
    }

    // New unified interface methods
    public async Task<RedditListing<RedditSubmission>> GetSubredditPostsAsync(string subreddit, string sort = "hot", int limit = 25)
    {
        await Task.Delay(200); // Simulate API delay
        
        var filteredPosts = _mockSubmissions
            .Where(s => s.Subreddit.Equals(subreddit, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();

        return new RedditListing<RedditSubmission>
        {
            Data = new RedditListingData<RedditSubmission>
            {
                Children = filteredPosts.Select(p => new RedditThing<RedditSubmission>
                {
                    Kind = "t3",
                    Data = p
                }).ToList(),
                After = filteredPosts.Count >= limit ? "next_page_token" : null,
                Before = null
            }
        };
    }

    public async Task<RedditSubmission?> GetSubmissionAsync(string submissionId)
    {
        await Task.Delay(100); // Simulate API delay
        return _mockSubmissions.FirstOrDefault(s => s.Id == submissionId);
    }

    public async Task<RedditListing<RedditComment>> GetSubmissionCommentsAsync(string submissionId, string sort = "top", int limit = 100)
    {
        await Task.Delay(150); // Simulate API delay
        
        var comments = _mockComments
            .Where(c => c.LinkId == $"t3_{submissionId}")
            .Take(limit)
            .ToList();

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
        await Task.Delay(300); // Simulate API delay
        
        var filteredPosts = _mockSubmissions.AsEnumerable();
        
        if (!string.IsNullOrEmpty(subreddit))
        {
            filteredPosts = filteredPosts.Where(s => s.Subreddit.Equals(subreddit, StringComparison.OrdinalIgnoreCase));
        }
        
        filteredPosts = filteredPosts.Where(s => 
            s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            s.SelfText.Contains(query, StringComparison.OrdinalIgnoreCase));

        var results = filteredPosts.Take(limit).ToList();

        return new RedditListing<RedditSubmission>
        {
            Data = new RedditListingData<RedditSubmission>
            {
                Children = results.Select(p => new RedditThing<RedditSubmission>
                {
                    Kind = "t3",
                    Data = p
                }).ToList(),
                After = results.Count >= limit ? "next_page_token" : null,
                Before = null
            }
        };
    }
}
