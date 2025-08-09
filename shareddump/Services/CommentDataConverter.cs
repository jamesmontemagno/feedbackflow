using SharedDump.Models;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Models.DevBlogs;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.Reddit;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.YouTube;

namespace SharedDump.Services;

/// <summary>
/// Service for converting platform-specific comment models to common CommentData structures
/// </summary>
public static class CommentDataConverter
{
    /// <summary>
    /// Converts YouTube video data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertYouTube(List<YouTubeOutputVideo> videos)
    {
        return videos.Select(video => new CommentThread
        {
            Id = video.Id,
            Title = video.Title ?? "Untitled Video",
            Description = video.Description,
            Author = video.ChannelTitle,
            CreatedAt = video.PublishedAt.DateTime,
            Url = video.Url,
            SourceType = "YouTube",
            Metadata = new Dictionary<string, object>
            {
                ["ChannelId"] = video.ChannelId,
                ["ViewCount"] = video.ViewCount,
                ["LikeCount"] = video.LikeCount,
                ["CommentCount"] = video.CommentCount
            },
            Comments = ConvertYouTubeComments(video.Comments)
        }).ToList();
    }    private static List<CommentData> ConvertYouTubeComments(List<YouTubeOutputComment> comments)
    {
        // First pass: Create all comment objects
        var commentDict = new Dictionary<string, CommentData>();
        foreach (var c in comments)
        {
            commentDict[c.Id] = new CommentData
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Author = c.Author ?? "Unknown",
                Content = c.Text ?? string.Empty,
                CreatedAt = c.PublishedAt
            };
        }

        // Second pass: Build the hierarchy
        var rootComments = new List<CommentData>();
        var orphanedComments = new List<CommentData>();
        
        foreach (var comment in commentDict.Values)
        {
            if (string.IsNullOrEmpty(comment.ParentId))
            {
                // This is a root comment
                rootComments.Add(comment);
            }
            else if (commentDict.TryGetValue(comment.ParentId, out var parent))
            {
                // This is a reply to a comment we have
                parent.Replies.Add(comment);
            }
            else
            {
                // This is an orphaned comment (parent not in our data)
                // Create a new comment object with modified content to indicate it's an orphaned reply
                var orphanedComment = new CommentData
                {
                    Id = comment.Id,
                    ParentId = comment.ParentId,
                    Author = comment.Author,
                    Content = $"[Reply to unavailable comment] {comment.Content}",
                    CreatedAt = comment.CreatedAt,
                    Score = comment.Score,
                    Url = comment.Url,
                    Metadata = comment.Metadata,
                    Replies = comment.Replies
                };
                
                // Add directly to root comments to preserve it in the export
                rootComments.Add(orphanedComment);
            }
        }

        return rootComments;
    }

    /// <summary>
    /// Converts Reddit thread data to comment threads
    /// </summary>
    public static CommentThread ConvertReddit(RedditThreadModel thread)
    {
        return new CommentThread
        {
            Id = thread.Id,
            Title = thread.Title,
            Description = thread.SelfText,
            Author = thread.Author,
            CreatedAt = thread.CreatedUtc.DateTime,
            Url = thread.Url,
            SourceType = "Reddit",
            Metadata = new Dictionary<string, object>
            {
                ["Subreddit"] = thread.Subreddit,
                ["Score"] = thread.Score,
                ["UpvoteRatio"] = thread.UpvoteRatio,
                ["NumComments"] = thread.NumComments,
                ["Permalink"] = thread.Permalink
            },
            Comments = ConvertRedditComments(thread.Comments)
        };
    }

    // Overload to support previous list-based usage patterns
    public static List<CommentThread> ConvertReddit(List<RedditThreadModel> threads)
    {
        var list = new List<CommentThread>(threads.Count);
        foreach (var t in threads)
            list.Add(ConvertReddit(t));
        return list;
    }

    private static List<CommentData> ConvertRedditComments(List<RedditCommentModel> comments)
    {
        return comments.Select(comment => new CommentData
        {
            Id = comment.Id,
            ParentId = comment.ParentId,
            Author = comment.Author,
            Content = comment.Body,
            CreatedAt = comment.CreatedUtc.DateTime,
            Score = comment.Score,
            Replies = comment.Replies != null ? ConvertRedditComments(comment.Replies) : new List<CommentData>()
        }).ToList();
    }

    /// <summary>
    /// Converts GitHub issue data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertGitHubIssues(List<GithubIssueModel> issues)
    {
        return issues.Select(issue => new CommentThread
        {
            Id = issue.Id,
            Title = issue.Title,
            Description = issue.Body,
            Author = issue.Author,
            CreatedAt = issue.CreatedAt,
            Url = issue.URL,
            SourceType = "GitHub Issue",
            Metadata = new Dictionary<string, object>
            {
                ["Upvotes"] = issue.Upvotes,
                ["Labels"] = issue.Labels.ToList(),
                ["LastUpdated"] = issue.LastUpdated
            },
            Comments = ConvertGitHubComments(issue.Comments)
        }).ToList();
    }

    /// <summary>
    /// Converts GitHub discussion data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertGitHubDiscussions(List<GithubDiscussionModel> discussions)
    {
        return discussions.Select(discussion => new CommentThread
        {
            Id = discussion.Title.GetHashCode().ToString(), // Discussions don't seem to have IDs
            Title = discussion.Title,
            Description = string.Empty,
            Author = "Unknown", // Not provided in the model
            CreatedAt = DateTime.UtcNow, // Not provided in the model
            Url = discussion.Url,
            SourceType = "GitHub Discussion",
            Metadata = discussion.AnswerId != null ? new Dictionary<string, object> { ["AnswerId"] = discussion.AnswerId } : null,
            Comments = ConvertGitHubComments(discussion.Comments)
        }).ToList();
    }

    private static List<CommentData> ConvertGitHubComments(GithubCommentModel[] comments)
    {
        // Build hierarchy based on parent relationships
        var commentDict = comments.ToDictionary(c => c.Id, c => new CommentData
        {
            Id = c.Id,
            ParentId = c.ParentId,
            Author = c.Author,
            Content = c.Content,
            CreatedAt = DateTime.TryParse(c.CreatedAt, out var date) ? date : DateTime.UtcNow,
            Url = c.Url,
            Metadata = CreateGitHubCommentMetadata(c)
        });        // Build the hierarchy
        var rootComments = new List<CommentData>();
        var orphanedComments = new List<CommentData>();
        
        foreach (var comment in commentDict.Values)
        {
            if (string.IsNullOrEmpty(comment.ParentId))
            {
                rootComments.Add(comment);
            }
            else if (commentDict.TryGetValue(comment.ParentId, out var parent))
            {
                parent.Replies.Add(comment);
            }
            else
            {
                // This is an orphaned comment (parent not in our data)
                orphanedComments.Add(new CommentData
                {
                    Id = comment.Id,
                    ParentId = comment.ParentId,
                    Author = comment.Author,
                    Content = $"[Reply to unavailable comment] {comment.Content}",
                    CreatedAt = comment.CreatedAt,
                    Score = comment.Score,
                    Url = comment.Url,
                    Metadata = comment.Metadata,
                    Replies = comment.Replies
                });
            }
        }
        
        // Add orphaned comments to root level
        foreach (var orphan in orphanedComments)
        {
            rootComments.Add(orphan);
        }

        return rootComments;
    }

    private static Dictionary<string, object>? CreateGitHubCommentMetadata(GithubCommentModel comment)
    {
        var metadata = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(comment.CodeContext))
            metadata["CodeContext"] = comment.CodeContext;
        
        if (!string.IsNullOrEmpty(comment.FilePath))
            metadata["FilePath"] = comment.FilePath;
        
        if (comment.LinePosition.HasValue)
            metadata["LinePosition"] = comment.LinePosition.Value;

        return metadata.Any() ? metadata : null;
    }

    /// <summary>
    /// Converts DevBlogs article data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertDevBlogs(DevBlogsArticleModel article)
    {
        return new List<CommentThread>
        {
            new CommentThread
            {
                Id = article.Url?.GetHashCode().ToString() ?? Guid.NewGuid().ToString(),
                Title = article.Title ?? "Untitled Article",
                Description = string.Empty,
                Author = "Unknown", // Not provided in the model
                CreatedAt = DateTime.UtcNow, // Not provided in the model
                Url = article.Url,
                SourceType = "DevBlogs",
                Comments = ConvertDevBlogsComments(article.Comments)
            }
        };
    }

    private static List<CommentData> ConvertDevBlogsComments(List<DevBlogsCommentModel> comments)
    {
        return comments.Select(comment => new CommentData
        {
            Id = comment.Id ?? Guid.NewGuid().ToString(),
            ParentId = comment.ParentId,
            Author = comment.Author ?? "Unknown",
            Content = comment.BodyHtml ?? string.Empty,
            CreatedAt = comment.PublishedUtc?.DateTime ?? DateTime.UtcNow,
            Replies = ConvertDevBlogsComments(comment.Replies)
        }).ToList();
    }

    /// <summary>
    /// Converts BlueSky feedback data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertBlueSky(BlueSkyFeedbackResponse response)
    {
        // Group root posts and their replies
        var rootPosts = response.Items.Where(item => string.IsNullOrEmpty(item.ParentId)).ToList();
        
        return rootPosts.Select(post => new CommentThread
        {
            Id = post.Id,
            Title = TruncateForTitle(post.Content),
            Description = post.Content,
            Author = post.AuthorName ?? post.Author,
            CreatedAt = post.TimestampUtc,
            Url = string.Empty, // URL not provided in the model
            SourceType = "BlueSky",
            Metadata = new Dictionary<string, object>
            {
                ["AuthorUsername"] = post.AuthorUsername ?? post.Author,
                ["ProcessedPostCount"] = response.ProcessedPostCount,
                ["MayBeIncomplete"] = response.MayBeIncomplete
            },
            Comments = ConvertBlueSkyComments(response.Items.Where(item => item.ParentId == post.Id).ToList())
        }).ToList();
    }

    private static string TruncateForTitle(string content, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content ?? "Untitled Post";
        
        return content.Substring(0, maxLength) + "...";
    }

    private static List<CommentData> ConvertBlueSkyComments(List<BlueSkyFeedbackItem> items)
    {
        return items.Select(item => new CommentData
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Author = item.AuthorName ?? item.Author,
            Content = item.Content,
            CreatedAt = item.TimestampUtc,
            Metadata = !string.IsNullOrEmpty(item.AuthorUsername) ? 
                new Dictionary<string, object> { ["AuthorUsername"] = item.AuthorUsername } : null,
            Replies = item.Replies != null ? ConvertBlueSkyComments(item.Replies) : new List<CommentData>()
        }).ToList();
    }

    /// <summary>
    /// Converts HackerNews item data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertHackerNews(List<HackerNewsItem> stories)
    {
        var threads = new List<CommentThread>();

        foreach (var story in stories)
        {
            // Skip deleted or invalid stories, and only process actual stories (not comments)
            if (story.Deleted == true || string.IsNullOrEmpty(story.Title) || story.Type != "story")
                continue;

            var thread = new CommentThread
            {
                Id = story.Id.ToString(),
                Title = story.Title,
                Description = story.Text,
                Author = story.By ?? "Unknown",
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(story.Time).DateTime,
                Url = story.Url,
                SourceType = "HackerNews",
                Metadata = new Dictionary<string, object>
                {
                    ["Score"] = story.Score ?? 0,
                    ["Descendants"] = story.Descendants ?? 0,
                    ["Type"] = story.Type ?? "story"
                },
                Comments = ConvertHackerNewsComments(stories, story.Kids)
            };

            threads.Add(thread);
        }

        return threads;
    }

    private static List<CommentData> ConvertHackerNewsComments(List<HackerNewsItem> allItems, List<int> commentIds)
    {
        if (commentIds == null || !commentIds.Any())
            return new List<CommentData>();

        var itemDict = allItems.ToDictionary(item => item.Id, item => item);
        var comments = new List<CommentData>();

        foreach (var commentId in commentIds)
        {
            if (itemDict.TryGetValue(commentId, out var item))
            {
                // Skip deleted comments
                if (item.Deleted == true)
                    continue;

                var comment = new CommentData
                {
                    Id = item.Id.ToString(),
                    ParentId = item.Parent?.ToString(),
                    Author = item.By ?? "Unknown",
                    Content = item.Text ?? string.Empty,
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(item.Time).DateTime,
                    Score = item.Score,
                    Replies = ConvertHackerNewsComments(allItems, item.Kids)
                };

                comments.Add(comment);
            }
        }

        return comments;
    }

    /// <summary>
    /// Converts Twitter/X feedback data to comment threads
    /// </summary>
    public static List<CommentThread> ConvertTwitter(TwitterFeedbackResponse response)
    {
        // Group root tweets and their replies
        var rootTweets = response.Items.Where(item => string.IsNullOrEmpty(item.ParentId)).ToList();
        
        return rootTweets.Select(tweet => new CommentThread
        {
            Id = tweet.Id,
            Title = TruncateForTitle(tweet.Content),
            Description = tweet.Content,
            Author = tweet.AuthorName ?? tweet.Author,
            CreatedAt = tweet.TimestampUtc,
            Url = string.Empty, // URL not provided in the model
            SourceType = "Twitter",
            Metadata = new Dictionary<string, object>
            {
                ["AuthorUsername"] = tweet.AuthorUsername ?? tweet.Author,
                ["ProcessedTweetCount"] = response.ProcessedTweetCount,
                ["MayBeIncomplete"] = response.MayBeIncomplete,
                ["RateLimitInfo"] = response.RateLimitInfo ?? string.Empty
            },
            Comments = tweet.Replies != null ? ConvertTwitterComments(tweet.Replies) : new List<CommentData>()
        }).ToList();
    }

    private static List<CommentData> ConvertTwitterComments(List<TwitterFeedbackItem> items)
    {
        return items.Select(item => new CommentData
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Author = item.AuthorName ?? item.Author,
            Content = item.Content,
            CreatedAt = item.TimestampUtc,
            Score = 0, // Twitter doesn't provide score/likes in this model
            Replies = item.Replies != null ? ConvertTwitterComments(item.Replies) : new List<CommentData>(),
            Metadata = new Dictionary<string, object>
            {
                ["AuthorUsername"] = item.AuthorUsername ?? item.Author
            }
        }).ToList();
    }

    /// <summary>
    /// Converts mixed additional data to comment threads based on type
    /// </summary>
    public static List<CommentThread> ConvertAdditionalData(object? additionalData)
    {
        return additionalData switch
        {
            List<YouTubeOutputVideo> videos => ConvertYouTube(videos),
            RedditThreadModel redditThread => new List<CommentThread> { ConvertReddit(redditThread) },
            List<RedditThreadModel> redditThreads => ConvertReddit(redditThreads),
            List<GithubIssueModel> issues => ConvertGitHubIssues(issues),
            List<GithubDiscussionModel> discussions => ConvertGitHubDiscussions(discussions),
            DevBlogsArticleModel article => ConvertDevBlogs(article),
            BlueSkyFeedbackResponse response => ConvertBlueSky(response),
            TwitterFeedbackResponse twitterResponse => ConvertTwitter(twitterResponse),
            List<HackerNewsItem> stories => ConvertHackerNews(stories),
            _ => new List<CommentThread>()
        };
    }
}