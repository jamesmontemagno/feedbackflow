using SharedDump.Models.BlueSkyFeedback.ApiModels;

namespace SharedDump.Models.BlueSkyFeedback.Converters;

/// <summary>
/// Converts BlueSky API models to BlueSkyFeedback models
/// </summary>
public static class BlueSkyModelConverter
{
    /// <summary>
    /// Converts a BlueSkyThreadResponse to a BlueSkyFeedbackResponse
    /// </summary>
    /// <param name="threadResponse">The API response to convert</param>
    /// <param name="mayBeIncomplete">Whether the response may be incomplete due to rate limiting</param>
    /// <param name="processedPostCount">The number of posts processed</param>
    /// <param name="rateLimitInfo">Rate limit information if applicable</param>
    /// <returns>A BlueSkyFeedbackResponse containing the converted data</returns>
    public static BlueSkyFeedbackResponse ConvertToFeedbackResponse(
        BlueSkyThreadResponse threadResponse, 
        bool mayBeIncomplete = false,
        int processedPostCount = 0,
        string? rateLimitInfo = null)
    {
        var response = new BlueSkyFeedbackResponse
        {
            Items = new List<BlueSkyFeedbackItem>(),
            MayBeIncomplete = mayBeIncomplete,
            ProcessedPostCount = processedPostCount,
            RateLimitInfo = rateLimitInfo
        };
        
        if (threadResponse?.Thread?.Post != null)
        {
            // Convert the main post
            var mainPost = ConvertToFeedbackItem(threadResponse.Thread.Post);
            
            // Process all replies recursively
            var allReplies = new List<BlueSkyFeedbackItem>();
            ProcessReplies(threadResponse.Thread, allReplies);
            
            // Organize into tree structure
            OrganizeRepliesIntoTree(mainPost, allReplies);
            
            response.Items.Add(mainPost);
        }
        
        return response;
    }
    
    /// <summary>
    /// Converts a BlueSkyPost to a BlueSkyFeedbackItem
    /// </summary>
    /// <param name="post">The BlueSky post to convert</param>
    /// <param name="parentId">Optional parent post ID if this is a reply</param>
    /// <returns>A BlueSkyFeedbackItem representing the post</returns>
    public static BlueSkyFeedbackItem ConvertToFeedbackItem(BlueSkyPost post, string? parentId = null)
    {
        return new BlueSkyFeedbackItem
        {
            Id = post.Uri ?? string.Empty,
            Author = post.Author?.Did ?? string.Empty,
            AuthorName = post.Author?.DisplayName,
            AuthorUsername = post.Author?.Handle,
            Content = post.Record?.Text ?? string.Empty,
            TimestampUtc = post.IndexedAt,
            ParentId = parentId,
            Replies = new List<BlueSkyFeedbackItem>()
        };
    }
    
    /// <summary>
    /// Recursively processes replies from a thread view
    /// </summary>
    /// <param name="threadView">The thread view to process</param>
    /// <param name="allReplies">List to collect all replies</param>
    private static void ProcessReplies(BlueSkyThreadView threadView, List<BlueSkyFeedbackItem> allReplies)
    {
        if (threadView.Replies == null || !threadView.Replies.Any())
        {
            return;
        }
        
        foreach (var replyView in threadView.Replies)
        {
            if (replyView.Post != null)
            {
                // Get the parent ID from the reply itself
                var parentId = ExtractParentIdFromPost(replyView.Post);
                
                // If parent ID is empty, use the original post's URI
                if (string.IsNullOrEmpty(parentId) && threadView.Post != null)
                {
                    parentId = threadView.Post.Uri;
                }
                
                // Convert the reply to a feedback item
                var replyItem = ConvertToFeedbackItem(replyView.Post, parentId);
                allReplies.Add(replyItem);
                
                // Process nested replies recursively
                ProcessReplies(replyView, allReplies);
            }
        }
    }
    
    /// <summary>
    /// Extracts the parent ID from a post record
    /// </summary>
    /// <param name="post">The post to extract the parent ID from</param>
    /// <returns>The parent ID or null if not found</returns>
    private static string? ExtractParentIdFromPost(BlueSkyPost post)
    {
        if (post.Record?.Reply?.Parent?.Uri != null)
        {
            return post.Record.Reply.Parent.Uri;
        }
        
        return null;
    }
    
    /// <summary>
    /// Organizes a flat list of replies into a proper tree structure based on ParentId
    /// </summary>
    /// <param name="rootPost">The root post to attach replies to</param>
    /// <param name="allReplies">All replies to organize</param>
    private static void OrganizeRepliesIntoTree(BlueSkyFeedbackItem rootPost, List<BlueSkyFeedbackItem> allReplies)
    {
        // Ensure lists are initialized
        rootPost.Replies ??= new List<BlueSkyFeedbackItem>();
        foreach (var reply in allReplies)
        {
            reply.Replies ??= new List<BlueSkyFeedbackItem>();
        }
        
        // Create a dictionary for quick lookup of posts by ID
        var postDict = new Dictionary<string, BlueSkyFeedbackItem>();
        postDict[rootPost.Id] = rootPost;
        
        // First pass: add all replies to the dictionary
        foreach (var reply in allReplies)
        {
            if (!string.IsNullOrEmpty(reply.Id) && !postDict.ContainsKey(reply.Id))
            {
                postDict[reply.Id] = reply;
            }
        }
        
        // Second pass: organize replies by parent ID
        foreach (var reply in allReplies)
        {
            // Skip if ParentId is empty or if it's the same as the reply ID (should never happen)
            if (string.IsNullOrEmpty(reply.ParentId) || reply.ParentId == reply.Id)
            {
                // If there's no valid parent, attach to root as a fallback
                if (rootPost.Replies != null && !rootPost.Replies.Any(r => r.Id == reply.Id))
                {
                    rootPost.Replies.Add(reply);
                }
                continue;
            }
            
            // If the parent exists in our dictionary, add this reply to its children
            if (postDict.TryGetValue(reply.ParentId, out var parent))
            {
                parent.Replies ??= new List<BlueSkyFeedbackItem>();
                if (!parent.Replies.Any(r => r.Id == reply.Id))
                {
                    parent.Replies.Add(reply);
                }
            }
            else
            {
                // If we can't find the parent (it might be outside our search window),
                // attach to the root post as a fallback
                rootPost.Replies ??= new List<BlueSkyFeedbackItem>();
                if (!rootPost.Replies.Any(r => r.Id == reply.Id))
                {
                    // Only add to root if we can't find a better place
                    rootPost.Replies.Add(reply);
                }
            }
        }
        
        // Sort replies by timestamp
        SortRepliesByTimestamp(rootPost);
    }
    
    /// <summary>
    /// Recursively sorts replies by timestamp (oldest first)
    /// </summary>
    /// <param name="post">The post whose replies to sort</param>
    private static void SortRepliesByTimestamp(BlueSkyFeedbackItem post)
    {
        if (post?.Replies == null || !post.Replies.Any())
        {
            return;
        }
        
        // Sort the immediate replies by timestamp
        post.Replies = post.Replies
            .OrderBy(r => r.TimestampUtc)
            .ToList();
            
        // Recursively sort nested replies
        foreach (var reply in post.Replies)
        {
            SortRepliesByTimestamp(reply);
        }
    }
}
