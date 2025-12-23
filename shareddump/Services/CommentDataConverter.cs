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
    /// <param name="videos">List of YouTube videos with comments</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertYouTube(List<YouTubeOutputVideo> videos, bool forAnalysis = false)
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
            Metadata = forAnalysis ? null : new Dictionary<string, object>
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
    /// <param name="thread">Reddit thread to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static CommentThread ConvertReddit(RedditThreadModel thread, bool forAnalysis = false)
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
            Metadata = forAnalysis ? null : new Dictionary<string, object>
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
    public static List<CommentThread> ConvertReddit(List<RedditThreadModel> threads, bool forAnalysis = false)
    {
        var list = new List<CommentThread>(threads.Count);
        foreach (var t in threads)
            list.Add(ConvertReddit(t, forAnalysis));
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
    /// <param name="issues">List of GitHub issues to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertGitHubIssues(List<GithubIssueModel> issues, bool forAnalysis = false)
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
            Metadata = forAnalysis ? null : new Dictionary<string, object>
            {
                ["Upvotes"] = issue.Upvotes,
                ["Labels"] = issue.Labels.ToList(),
                ["LastUpdated"] = issue.LastUpdated
            },
            Comments = ConvertGitHubComments(issue.Comments, forAnalysis)
        }).ToList();
    }

    /// <summary>
    /// Converts GitHub discussion data to comment threads
    /// </summary>
    /// <param name="discussions">List of GitHub discussions to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertGitHubDiscussions(List<GithubDiscussionModel> discussions, bool forAnalysis = false)
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
            Metadata = (forAnalysis || discussion.AnswerId == null) ? null : new Dictionary<string, object> { ["AnswerId"] = discussion.AnswerId },
            Comments = ConvertGitHubComments(discussion.Comments, forAnalysis)
        }).ToList();
    }

    private static List<CommentData> ConvertGitHubComments(GithubCommentModel[] comments, bool forAnalysis = false)
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
            Metadata = CreateGitHubCommentMetadata(c, forAnalysis)
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

    private static Dictionary<string, object>? CreateGitHubCommentMetadata(GithubCommentModel comment, bool forAnalysis = false)
    {
        // When analyzing, exclude code review fields to reduce token usage
        if (forAnalysis)
            return null;
            
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
    /// <param name="article">DevBlogs article to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertDevBlogs(DevBlogsArticleModel article, bool forAnalysis = false)
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
    /// <param name="response">BlueSky feedback response to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertBlueSky(BlueSkyFeedbackResponse response, bool forAnalysis = false)
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
            Metadata = forAnalysis ? null : new Dictionary<string, object>
            {
                ["AuthorUsername"] = post.AuthorUsername ?? post.Author,
                ["ProcessedPostCount"] = response.ProcessedPostCount,
                ["MayBeIncomplete"] = response.MayBeIncomplete
            },
            Comments = ConvertBlueSkyCommentsRecursive(response.Items, post.Id, forAnalysis)
        }).ToList();
    }

    private static List<CommentData> ConvertBlueSkyCommentsRecursive(List<BlueSkyFeedbackItem> allItems, string parentId, bool forAnalysis = false)
    {
        var directReplies = allItems.Where(item => item.ParentId == parentId).ToList();
        
        return directReplies.Select(item => new CommentData
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Author = item.AuthorName ?? item.Author,
            Content = item.Content,
            CreatedAt = item.TimestampUtc,
            Metadata = (forAnalysis || string.IsNullOrEmpty(item.AuthorUsername)) ? null :
                new Dictionary<string, object> { ["AuthorUsername"] = item.AuthorUsername },
            Replies = ConvertBlueSkyCommentsRecursive(allItems, item.Id, forAnalysis) // Recursive call for nested replies
        }).ToList();
    }

    private static string TruncateForTitle(string content, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
            return content ?? "Untitled Post";
        
        return content.Substring(0, maxLength) + "...";
    }



    /// <summary>
    /// Converts HackerNews item data to comment threads
    /// </summary>
    /// <param name="items">List of HackerNews items (first item is the story, rest are comments)</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertHackerNews(List<HackerNewsItem> items, bool forAnalysis = false)
    {
        var threads = new List<CommentThread>();

        if (items == null || !items.Any())
            return threads;

        // The first item in the list is typically the story, rest are comments
        // Find the story (item with Type == "story") or use the first item if it has a title
        var story = items.FirstOrDefault(i => i.Type == "story") 
                    ?? items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Title));
        
        if (story == null || story.Deleted == true)
        {
            // If no story found, try to create a thread from all comments
            // This handles cases where we only have comments without the parent story
            if (items.Any(i => i.Type == "comment"))
            {
                var firstComment = items.First(i => i.Type == "comment");
                var thread = new CommentThread
                {
                    Id = firstComment.MainStoryId?.ToString() ?? firstComment.Id.ToString(),
                    Title = "HackerNews Discussion",
                    Description = null,
                    Author = "Unknown",
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(firstComment.Time).DateTime,
                    Url = $"https://news.ycombinator.com/item?id={firstComment.MainStoryId ?? firstComment.Id}",
                    SourceType = "HackerNews",
                    Metadata = null,
                    Comments = ConvertHackerNewsCommentsFromFlatList(items)
                };
                threads.Add(thread);
            }
            return threads;
        }

        var thread2 = new CommentThread
        {
            Id = story.Id.ToString(),
            Title = story.Title ?? "HackerNews Discussion",
            Description = story.Text,
            Author = story.By ?? "Unknown",
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(story.Time).DateTime,
            Url = story.Url ?? $"https://news.ycombinator.com/item?id={story.Id}",
            SourceType = "HackerNews",
            Metadata = forAnalysis ? null : new Dictionary<string, object>
            {
                ["Score"] = story.Score ?? 0,
                ["Descendants"] = story.Descendants ?? 0,
                ["Type"] = story.Type ?? "story"
            },
            Comments = story.Kids != null && story.Kids.Any() 
                ? ConvertHackerNewsComments(items, story.Kids)
                : ConvertHackerNewsCommentsFromFlatList(items.Where(i => i.Id != story.Id).ToList())
        };

        threads.Add(thread2);
        return threads;
    }

    /// <summary>
    /// Converts a flat list of HackerNews comments to CommentData, building the tree structure from Parent references
    /// </summary>
    private static List<CommentData> ConvertHackerNewsCommentsFromFlatList(List<HackerNewsItem> items)
    {
        if (items == null || !items.Any())
            return new List<CommentData>();

        // Filter to only comments
        var comments = items.Where(i => i.Type == "comment" && i.Deleted != true).ToList();
        if (!comments.Any())
            return new List<CommentData>();

        // Create CommentData objects for all comments
        var commentDataDict = comments.ToDictionary(
            c => c.Id,
            c => new CommentData
            {
                Id = c.Id.ToString(),
                ParentId = c.Parent?.ToString(),
                Author = c.By ?? "Unknown",
                Content = CleanHtmlContent(c.Text ?? string.Empty),
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(c.Time).DateTime,
                Score = c.Score,
                Replies = new List<CommentData>()
            });

        // Build the tree structure
        var rootComments = new List<CommentData>();
        foreach (var comment in comments)
        {
            var commentData = commentDataDict[comment.Id];
            
            // Check if parent is another comment in our list
            if (comment.Parent.HasValue && commentDataDict.TryGetValue(comment.Parent.Value, out var parentComment))
            {
                parentComment.Replies.Add(commentData);
            }
            else
            {
                // This is a root-level comment (parent is the story or not in our list)
                rootComments.Add(commentData);
            }
        }

        return rootComments;
    }

    /// <summary>
    /// Cleans HTML entities and tags from HackerNews content
    /// </summary>
    private static string CleanHtmlContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Decode common HTML entities
        content = content
            .Replace("&#x27;", "'")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#x2F;", "/")
            .Replace("<p>", "\n\n")
            .Replace("</p>", "")
            .Replace("<i>", "_")
            .Replace("</i>", "_")
            .Replace("<b>", "**")
            .Replace("</b>", "**")
            .Replace("<code>", "`")
            .Replace("</code>", "`")
            .Replace("<pre>", "```\n")
            .Replace("</pre>", "\n```");

        // Remove remaining HTML tags (like <a> links)
        content = System.Text.RegularExpressions.Regex.Replace(content, @"<a[^>]*href=""([^""]+)""[^>]*>([^<]*)</a>", "$2 ($1)");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "");

        return content.Trim();
    }

    private static List<CommentData> ConvertHackerNewsComments(List<HackerNewsItem> allItems, List<int>? commentIds)
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
                    Content = CleanHtmlContent(item.Text ?? string.Empty),
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
    /// <param name="response">Twitter feedback response to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertTwitter(TwitterFeedbackResponse response, bool forAnalysis = false)
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
            Metadata = forAnalysis ? null : new Dictionary<string, object>
            {
                ["AuthorUsername"] = tweet.AuthorUsername ?? tweet.Author,
                ["ProcessedTweetCount"] = response.ProcessedTweetCount,
                ["MayBeIncomplete"] = response.MayBeIncomplete,
                ["RateLimitInfo"] = response.RateLimitInfo ?? string.Empty
            },
            Comments = tweet.Replies != null ? ConvertTwitterComments(tweet.Replies, forAnalysis) : new List<CommentData>()
        }).ToList();
    }

    private static List<CommentData> ConvertTwitterComments(List<TwitterFeedbackItem> items, bool forAnalysis = false)
    {
        return items.Select(item => new CommentData
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Author = item.AuthorName ?? item.Author,
            Content = item.Content,
            CreatedAt = item.TimestampUtc,
            Score = 0, // Twitter doesn't provide score/likes in this model
            Replies = item.Replies != null ? ConvertTwitterComments(item.Replies, forAnalysis) : new List<CommentData>(),
            Metadata = forAnalysis ? null : new Dictionary<string, object>
            {
                ["AuthorUsername"] = item.AuthorUsername ?? item.Author
            }
        }).ToList();
    }

    /// <summary>
    /// Converts mixed additional data to comment threads based on type
    /// </summary>
    /// <param name="additionalData">Platform-specific data to convert</param>
    /// <param name="forAnalysis">If true, excludes metadata to reduce token usage for AI analysis</param>
    public static List<CommentThread> ConvertAdditionalData(object? additionalData, bool forAnalysis = false)
    {
        return additionalData switch
        {
            List<YouTubeOutputVideo> videos => ConvertYouTube(videos, forAnalysis),
            RedditThreadModel redditThread => new List<CommentThread> { ConvertReddit(redditThread, forAnalysis) },
            List<RedditThreadModel> redditThreads => ConvertReddit(redditThreads, forAnalysis),
            List<GithubIssueModel> issues => ConvertGitHubIssues(issues, forAnalysis),
            List<GithubDiscussionModel> discussions => ConvertGitHubDiscussions(discussions, forAnalysis),
            DevBlogsArticleModel article => ConvertDevBlogs(article, forAnalysis),
            BlueSkyFeedbackResponse response => ConvertBlueSky(response, forAnalysis),
            TwitterFeedbackResponse twitterResponse => ConvertTwitter(twitterResponse, forAnalysis),
            List<HackerNewsItem> stories => ConvertHackerNews(stories, forAnalysis),
            _ => new List<CommentThread>()
        };
    }
}