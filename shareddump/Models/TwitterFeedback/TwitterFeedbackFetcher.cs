using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Utils;

namespace SharedDump.Models.TwitterFeedback;

/// <summary>
/// Service to fetch and transform Twitter/X feedback data.
/// </summary>
public class TwitterFeedbackFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwitterFeedbackFetcher> _logger;
    private bool _hitRateLimit = false;
    private int _maxReplySearches = 60; // Limit per 15 minutes
    private int _currentReplySearches = 0;
    private HashSet<string> _processedTweetIds = new();

    public TwitterFeedbackFetcher(HttpClient httpClient, ILogger<TwitterFeedbackFetcher> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }    private async Task<List<TwitterFeedbackItem>> FetchRepliesRecursivelyAsync(string conversationId, string parentTweetId, string authorId, CancellationToken cancellationToken)
    {
        // Check if we hit rate limits
        if (_hitRateLimit || _currentReplySearches >= _maxReplySearches)
        {
            _hitRateLimit = true;
            _logger.LogWarning("Rate limit reached for reply searches (60 per 15 minutes). Skipping replies for {ParentTweetId}", parentTweetId);
            return new List<TwitterFeedbackItem>();
        }

        // Skip if we've already processed this parent tweet
        if (_processedTweetIds.Contains(parentTweetId))
        {
            _logger.LogInformation("Skipping already processed tweet ID: {TweetId}", parentTweetId);
            return new List<TwitterFeedbackItem>();
        }

        // Add this tweet ID to processed set
        _processedTweetIds.Add(parentTweetId);

        var replies = new List<TwitterFeedbackItem>();
        var userDict = new Dictionary<string, (string name, string username)>(); // Keep userDict outside the loop to accumulate users from all pages
        string? nextToken = null;
        
        // Modified query to get all replies in the conversation
        // Instead of excluding the author, we get all tweets in the conversation
        var baseUrl = $"https://api.twitter.com/2/tweets/search/recent?query=conversation_id:{conversationId}&tweet.fields=author_id,created_at,conversation_id,in_reply_to_user_id,referenced_tweets&expansions=author_id,referenced_tweets.id&user.fields=name,username&max_results=100";

        do
        {
            // Increment search counter
            _currentReplySearches++;
            
            var repliesUrl = baseUrl;
            if (!string.IsNullOrEmpty(nextToken))
            {
                repliesUrl += $"&next_token={nextToken}";
            }

            var repliesResp = await _httpClient.GetAsync(repliesUrl, cancellationToken);

            if (!repliesResp.IsSuccessStatusCode)
            {
                // Check for rate limiting (status code 429)
                if ((int)repliesResp.StatusCode == 429)
                {
                    _hitRateLimit = true;
                    _logger.LogWarning("Rate limit reached when fetching replies for {ParentTweetId}", parentTweetId);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch replies page: {Status}. Stopping pagination for parent {ParentTweetId}.", repliesResp.StatusCode, parentTweetId);
                }
                break; // Stop pagination if a page fails
            }

            var repliesJson = await repliesResp.Content.ReadAsStringAsync(cancellationToken);
            using var repliesDoc = JsonDocument.Parse(repliesJson);
            var root = repliesDoc.RootElement;

            // Accumulate user information from includes.users
            if (root.TryGetProperty("includes", out var includes) &&
                includes.TryGetProperty("users", out var users))
            {
                foreach (var user in users.EnumerateArray())
                {
                    var userId = user.GetProperty("id").GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(userId) && !userDict.ContainsKey(userId)) // Add only if not already present
                    {
                        var name = user.GetProperty("name").GetString() ?? string.Empty;
                        var username = user.GetProperty("username").GetString() ?? string.Empty;
                        userDict[userId] = (name, username);
                    }
                }
            }

            // Process replies data
            if (root.TryGetProperty("data", out var repliesData))
            {
                foreach (var reply in repliesData.EnumerateArray())
                {
                    // Skip the main tweet itself
                    var replyId = reply.GetProperty("id").GetString() ?? string.Empty;
                    if (replyId == parentTweetId)
                    {
                        continue;
                    }
                    
                    // Skip if we've already processed this reply
                    if (!string.IsNullOrEmpty(replyId) && _processedTweetIds.Contains(replyId))
                    {
                        continue;
                    }
                    
                    // Mark this reply as processed
                    if (!string.IsNullOrEmpty(replyId))
                    {
                        _processedTweetIds.Add(replyId);
                    }
                    
                    var replyAuthorId = reply.GetProperty("author_id").GetString() ?? string.Empty;
                    // Use the accumulated userDict
                    var (authorName, authorUsername) = userDict.GetValueOrDefault(replyAuthorId, (string.Empty, string.Empty));

                    // Determine the parent tweet ID for this reply
                    var repliedToId = string.Empty;
                    
                    if (reply.TryGetProperty("referenced_tweets", out var refTweets))
                    {
                        foreach (var refTweet in refTweets.EnumerateArray())
                        {
                            if (refTweet.GetProperty("type").GetString() == "replied_to")
                            {
                                repliedToId = refTweet.GetProperty("id").GetString() ?? string.Empty;
                                break;
                            }
                        }
                    }

                    // If no parent found, skip this reply
                    if (string.IsNullOrEmpty(repliedToId))
                    {
                        continue;
                    }

                    // Create reply item
                    var replyItem = new TwitterFeedbackItem
                    {
                        Id = replyId,
                        Author = replyAuthorId,
                        AuthorName = authorName,
                        AuthorUsername = authorUsername,
                        Content = reply.GetProperty("text").GetString() ?? string.Empty,
                        TimestampUtc = reply.GetProperty("created_at").GetDateTime(),
                        ParentId = repliedToId,
                        Replies = new List<TwitterFeedbackItem>()
                    };

                    // Add all replies to our collection
                    // They will be properly organized later in the tree structure
                    replies.Add(replyItem);
                    
                    // Note: We no longer make the recursive call here, as we're getting all replies in one go
                }
            }

            // Check for next_token for pagination
            if (root.TryGetProperty("meta", out var meta) && meta.TryGetProperty("next_token", out var nextTokenElement))
            {
                nextToken = nextTokenElement.GetString();
            }
            else
            {
                nextToken = null; // No more pages
            }

        } while (!string.IsNullOrEmpty(nextToken) && !cancellationToken.IsCancellationRequested && !_hitRateLimit && _currentReplySearches < _maxReplySearches);

        return replies;
    }    public async Task<TwitterFeedbackResponse?> FetchFeedbackAsync(string tweetUrlOrId, CancellationToken cancellationToken = default)
    {
        // Reset tracking variables
        _hitRateLimit = false;
        _currentReplySearches = 0;
        _processedTweetIds.Clear();
        
        try
        {
            var tweetId = TwitterUrlParser.ExtractTweetId(tweetUrlOrId);
            if (string.IsNullOrEmpty(tweetId))
            {
                _logger.LogError("Invalid tweet URL or ID: {TweetUrlOrId}", tweetUrlOrId);
                return null;
            }

            // Fetch the main tweet with more comprehensive information
            var tweetResp = await _httpClient.GetAsync(
                $"https://api.twitter.com/2/tweets/{tweetId}?tweet.fields=author_id,created_at,conversation_id,referenced_tweets&expansions=author_id,referenced_tweets.id&user.fields=name,username", 
                cancellationToken);
                
            if (!tweetResp.IsSuccessStatusCode)
            {
                // Check for rate limiting (status code 429)
                if ((int)tweetResp.StatusCode == 429)
                {
                    _hitRateLimit = true;
                    _logger.LogWarning("Rate limit reached when fetching main tweet {TweetId}", tweetId);
                    return new TwitterFeedbackResponse
                    {
                        Items = new List<TwitterFeedbackItem>(),
                        MayBeIncomplete = true,
                        RateLimitInfo = "Rate limit reached. Twitter API allows 15 main tweet lookups per 15 minutes."
                    };
                }
                
                _logger.LogError("Failed to fetch tweet: {Status}", tweetResp.StatusCode);
                return null;
            }

            var tweetJson = await tweetResp.Content.ReadAsStringAsync(cancellationToken);
            using var tweetDoc = JsonDocument.Parse(tweetJson);
            var tweetRoot = tweetDoc.RootElement;
            var tweetData = tweetRoot.GetProperty("data");
            var authorId = tweetData.GetProperty("author_id").GetString();
            var conversationId = tweetData.GetProperty("conversation_id").GetString();
            var createdAt = tweetData.GetProperty("created_at").GetDateTime();
            var content = tweetData.GetProperty("text").GetString();

            // Get author information from includes
            var authorName = string.Empty;
            var authorUsername = string.Empty;
            if (tweetRoot.TryGetProperty("includes", out var includes) &&
                includes.TryGetProperty("users", out var users) &&
                users.EnumerateArray().Any())
            {
                var user = users.EnumerateArray().First();
                authorName = user.GetProperty("name").GetString() ?? string.Empty;
                authorUsername = user.GetProperty("username").GetString() ?? string.Empty;
            }

            // Create the main tweet object
            var mainTweet = new TwitterFeedbackItem
            {
                Id = tweetId,
                Author = authorId ?? string.Empty,
                AuthorName = authorName,
                AuthorUsername = authorUsername,
                Content = content ?? string.Empty,
                TimestampUtc = createdAt,
                Replies = new List<TwitterFeedbackItem>()
            };

            _logger.LogInformation("Fetching replies for tweet {TweetId} in conversation {ConversationId}", tweetId, conversationId);
            
            // First, we need to determine if this is the conversation root
            // If not, we should start with the conversation root to get all context
            var isConversationRoot = tweetId == conversationId || 
                                    !tweetData.TryGetProperty("referenced_tweets", out var _);
                                     
            string rootTweetId = isConversationRoot ? tweetId : conversationId ?? tweetId;

            // Fetch all replies for the conversation
            var allReplies = await FetchRepliesRecursivelyAsync(
                conversationId ?? string.Empty, 
                rootTweetId, 
                authorId ?? string.Empty, 
                cancellationToken);
            
            _logger.LogInformation("Found {ReplyCount} replies in conversation {ConversationId}", 
                allReplies.Count, conversationId);
            
            // Initialize Replies list if it's null
            mainTweet.Replies ??= new List<TwitterFeedbackItem>();
            
            // Organize replies into a tree structure
            OrganizeRepliesIntoTree(mainTweet, allReplies);

            var response = new TwitterFeedbackResponse
            {
                Items = new List<TwitterFeedbackItem> { mainTweet },
                MayBeIncomplete = _hitRateLimit || _currentReplySearches >= _maxReplySearches,
                ProcessedTweetCount = _processedTweetIds.Count
            };
            
            if (response.MayBeIncomplete)
            {
                response.RateLimitInfo = $"Some replies may be missing due to Twitter API rate limits (60 reply searches per 15 minutes). Processed {_processedTweetIds.Count} unique tweets.";
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitter feedback for {TweetUrlOrId}", tweetUrlOrId);
            return null;
        }
    }/// <summary>
    /// Organizes a flat list of replies into a proper tree structure based on ParentId
    /// </summary>
    private void OrganizeRepliesIntoTree(TwitterFeedbackItem rootTweet, List<TwitterFeedbackItem> allReplies)
    {
        // Ensure lists are initialized
        rootTweet.Replies ??= new List<TwitterFeedbackItem>();
        foreach (var reply in allReplies)
        {
            reply.Replies ??= new List<TwitterFeedbackItem>();
        }
        
        // Create a dictionary for quick lookup of tweets by ID
        var tweetDict = new Dictionary<string, TwitterFeedbackItem>();
        tweetDict[rootTweet.Id] = rootTweet;
        
        // First pass: add all replies to the dictionary
        foreach (var reply in allReplies)
        {
            if (!tweetDict.ContainsKey(reply.Id))
            {
                tweetDict[reply.Id] = reply;
            }
        }
        
        // Second pass: organize replies by parent ID
        foreach (var reply in allReplies)
        {
            // Skip if ParentId is empty or if it's the same as the reply ID (should never happen)
            if (string.IsNullOrEmpty(reply.ParentId) || reply.ParentId == reply.Id)
            {
                continue;
            }
            
            // If the parent exists in our dictionary, add this reply to its children
            if (tweetDict.TryGetValue(reply.ParentId, out var parent) && parent.Replies != null)
            {
                // Make sure we're not adding duplicates
                if (!parent.Replies.Any(r => r.Id == reply.Id))
                {
                    parent.Replies.Add(reply);
                }
            }
            else
            {
                // If we can't find the parent (it might be outside our search window),
                // attach to the root tweet as a fallback
                if (rootTweet.Replies != null && !rootTweet.Replies.Any(r => r.Id == reply.Id))
                {
                    // Only add to root if we can't find a better place
                    rootTweet.Replies.Add(reply);
                }
            }
        }
        
        // Sort replies by timestamp
        SortRepliesByTimestamp(rootTweet);
    }
    
    /// <summary>
    /// Recursively sorts replies by timestamp (oldest first)
    /// </summary>
    private void SortRepliesByTimestamp(TwitterFeedbackItem tweet)
    {
        if (tweet.Replies == null || !tweet.Replies.Any())
        {
            return;
        }
        
        // Sort the immediate replies by timestamp
        tweet.Replies = tweet.Replies
            .OrderBy(r => r.TimestampUtc)
            .ToList();
            
        // Recursively sort nested replies
        foreach (var reply in tweet.Replies)
        {
            SortRepliesByTimestamp(reply);
        }
    }

}
