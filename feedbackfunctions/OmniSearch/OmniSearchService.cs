using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.ContentSearch;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.HackerNews;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Services.Interfaces;

namespace FeedbackFunctions.OmniSearch;

/// <summary>
/// Service for aggregating search results from multiple platforms
/// </summary>
public class OmniSearchService
{
    private readonly ILogger<OmniSearchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IYouTubeService _youTubeService;
    private readonly IRedditService _redditService;
    private readonly IHackerNewsService _hackerNewsService;
    private readonly ITwitterService _twitterService;
    private readonly IBlueSkyService _blueSkyService;
    
    // In-memory cache for search results
    private static readonly ConcurrentDictionary<string, (OmniSearchResponse Response, DateTimeOffset CachedAt)> _cache = new();
    private readonly TimeSpan _cacheTTL;
    private readonly int _maxConcurrency;

    public OmniSearchService(
        ILogger<OmniSearchService> logger,
        IConfiguration configuration,
        IYouTubeService youTubeService,
        IRedditService redditService,
        IHackerNewsService hackerNewsService,
        ITwitterService twitterService,
        IBlueSkyService blueSkyService)
    {
        _logger = logger;
        _configuration = configuration;
        _youTubeService = youTubeService;
        _redditService = redditService;
        _hackerNewsService = hackerNewsService;
        _twitterService = twitterService;
        _blueSkyService = blueSkyService;

        // Read configuration with defaults
        _cacheTTL = TimeSpan.Parse(configuration["OmniSearch:CacheTTL"] ?? "00:05:00");
        _maxConcurrency = int.Parse(configuration["OmniSearch:MaxConcurrencyPerPlatform"] ?? "4");
    }

    /// <summary>
    /// Execute omni-search across multiple platforms
    /// </summary>
    public async Task<OmniSearchResponse> SearchAsync(
        OmniSearchRequest request, 
        SharedDump.Models.Authentication.AuthenticatedUser user,
        FeedbackFunctions.Services.Account.IUserAccountService userAccountService,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = GenerateCacheKey(request);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt < _cacheTTL)
            {
                _logger.LogInformation("Cache hit for omni-search query: {Query}", request.Query);
                // Still track usage even on cache hit - user is consuming the resource
                await TrackPlatformUsageAsync(user, userAccountService, request.Platforms.Count, request.Query);
                return cached.Response;
            }
            else
            {
                // Expired, remove
                _cache.TryRemove(cacheKey, out _);
            }
        }

        _logger.LogInformation("Executing omni-search for query: {Query} across platforms: {Platforms}", 
            request.Query, string.Join(", ", request.Platforms));

        var results = new List<OmniSearchResult>();
        var searchTasks = new List<Task<List<OmniSearchResult>>>();

        // Launch platform searches in parallel
        foreach (var platform in request.Platforms)
        {
            searchTasks.Add(SearchPlatformAsync(platform.ToLowerInvariant(), request, cancellationToken));
        }

        // Wait for all platforms to complete
        var platformResults = await Task.WhenAll(searchTasks);
        
        // Merge results
        foreach (var platformResult in platformResults)
        {
            results.AddRange(platformResult);
        }

        // Sort results (filtering for zero comments is handled on client side)
        results = request.SortMode.ToLowerInvariant() == "ranked"
            ? RankResults(results)
            : results.OrderByDescending(r => r.CommentCount).ThenByDescending(r => r.PublishedAt).ToList();

        // Don't apply pagination - return all results from all platforms
        // If user selects 2 platforms, they get 200 results (100 per platform)
        var totalCount = results.Count;

        var response = new OmniSearchResponse
        {
            Results = results, // Return all results, no pagination
            TotalCount = totalCount,
            Page = 1,
            PageSize = totalCount, // PageSize equals total count
            CachedAt = DateTimeOffset.UtcNow,
            Query = request.Query,
            PlatformsSearched = request.Platforms
        };

        // Cache the response
        _cache[cacheKey] = (response, DateTimeOffset.UtcNow);
        
        _logger.LogInformation("Omni-search completed. Found {Count} total results across {PlatformCount} platforms", 
            totalCount, request.Platforms.Count);

        // Track usage - one credit per platform searched
        await TrackPlatformUsageAsync(user, userAccountService, request.Platforms.Count, request.Query);

        return response;
    }

    /// <summary>
    /// Track usage for each platform searched
    /// </summary>
    private async Task TrackPlatformUsageAsync(
        SharedDump.Models.Authentication.AuthenticatedUser user,
        FeedbackFunctions.Services.Account.IUserAccountService userAccountService,
        int platformCount,
        string query)
    {
        try
        {
            // Track usage for each platform (amount = platformCount)
            await userAccountService.TrackUsageAsync(
                user.UserId, 
                SharedDump.Models.Account.UsageType.FeedQuery, 
                $"OmniSearch: {query}", 
                platformCount);
            
            _logger.LogInformation("Tracked {Count} usage credits for user {UserId} (OmniSearch)", platformCount, user.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking usage for user {UserId}", user.UserId);
            // Don't throw - tracking failures shouldn't break the search
        }
    }

    private async Task<List<OmniSearchResult>> SearchPlatformAsync(string platform, OmniSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return platform switch
            {
                "youtube" => await SearchYouTubeAsync(request, cancellationToken),
                "reddit" => await SearchRedditAsync(request, cancellationToken),
                "hackernews" or "hacker news" => await SearchHackerNewsAsync(request, cancellationToken),
                "twitter" => await SearchTwitterAsync(request, cancellationToken),
                "bluesky" => await SearchBlueSkyAsync(request, cancellationToken),
                _ => new List<OmniSearchResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching platform {Platform}", platform);
            return new List<OmniSearchResult>();
        }
    }

    private async Task<List<OmniSearchResult>> SearchYouTubeAsync(OmniSearchRequest request, CancellationToken cancellationToken)
    {
        var cutoffDate = request.FromDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var videos = await _youTubeService.SearchVideosBasicInfo(request.Query, "", cutoffDate);

        return videos
            .Take(request.MaxResults) // Limit to MaxResults per platform
            .Select(v => new OmniSearchResult
            {
                Id = $"youtube_{v.Id}",
                Title = v.Title ?? "Untitled",
                Snippet = TruncateText(v.Description, 200),
                Source = "YouTube",
                SourceId = v.Id,
                Url = v.Url ?? $"https://www.youtube.com/watch?v={v.Id}",
                PublishedAt = v.PublishedAt,
                Author = v.ChannelTitle,
                EngagementCount = v.ViewCount,
                CommentCount = (int)v.CommentCount
            }).ToList();
    }

    private async Task<List<OmniSearchResult>> SearchRedditAsync(OmniSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Reddit search across all subreddits or specific if filter applied
            var posts = await _redditService.SearchPostsAsync(request.Query, "", "relevance", request.MaxResults);

            if (posts?.Data?.Children == null)
            {
                _logger.LogWarning("Reddit search returned null or empty data");
                return new List<OmniSearchResult>();
            }

            return posts.Data.Children.Select(p => new OmniSearchResult
            {
                Id = $"reddit_{p.Data.Id}",
                Title = p.Data.Title,
                Snippet = TruncateText(p.Data.SelfText, 200),
                Source = "Reddit",
                SourceId = p.Data.Id,
                Url = $"https://reddit.com{p.Data.Permalink}",
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds((long)p.Data.CreatedUtc),
                Author = p.Data.Author,
                EngagementCount = p.Data.Score,
                CommentCount = p.Data.NumComments
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Reddit for query: {Query}", request.Query);
            return new List<OmniSearchResult>();
        }
    }

    private async Task<List<OmniSearchResult>> SearchHackerNewsAsync(OmniSearchRequest request, CancellationToken cancellationToken)
    {
        // Hacker News doesn't have a search API, so we get top stories and filter by keyword
        var topStoryIds = await _hackerNewsService.GetTopStoriesAsync();
        var results = new List<OmniSearchResult>();

        // Check more stories to ensure we can find enough matches (3x the requested amount)
        var storiesToCheck = topStoryIds.Take(Math.Min(300, request.MaxResults * 3));
        var semaphore = new SemaphoreSlim(_maxConcurrency);
        var tasks = new List<Task>();

        foreach (var storyId in storiesToCheck)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var story = await _hackerNewsService.GetStoryAsync(storyId);
                    if (story != null && 
                        (story.Title?.Contains(request.Query, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        lock (results)
                        {
                            results.Add(new OmniSearchResult
                            {
                                Id = $"hackernews_{story.Id}",
                                Title = story.Title ?? "Untitled",
                                Snippet = TruncateText(story.Url, 200),
                                Source = "HackerNews",
                                SourceId = story.Id.ToString(),
                                Url = story.Url ?? $"https://news.ycombinator.com/item?id={story.Id}",
                                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(story.Time),
                                Author = story.By,
                                EngagementCount = story.Score,
                                CommentCount = story.Descendants
                            });
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return results.OrderByDescending(r => r.EngagementCount).Take(request.MaxResults).ToList();
    }

    private async Task<List<OmniSearchResult>> SearchTwitterAsync(OmniSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tweets = await _twitterService.SearchTweetsAsync(
                request.Query,
                request.MaxResults,
                request.FromDate,
                request.ToDate,
                cancellationToken);

            return tweets.Select(t => new OmniSearchResult
            {
                Id = $"twitter_{t.Id}",
                Title = $"@{t.AuthorUsername}: {TruncateText(t.Content, 100)}",
                Snippet = TruncateText(t.Content, 200),
                Source = "Twitter",
                SourceId = t.Id,
                Url = $"https://twitter.com/{t.AuthorUsername}/status/{t.Id}",
                PublishedAt = t.TimestampUtc,
                Author = $"@{t.AuthorUsername}",
                EngagementCount = 0, // Engagement counts not stored in TwitterFeedbackItem
                CommentCount = CountReplies(t.Replies)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Twitter for query: {Query}", request.Query);
            return new List<OmniSearchResult>();
        }
    }

    private async Task<List<OmniSearchResult>> SearchBlueSkyAsync(OmniSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var posts = await _blueSkyService.SearchPostsAsync(
                request.Query,
                request.MaxResults,
                request.FromDate,
                cancellationToken);

            return posts.Select(p => new OmniSearchResult
            {
                Id = $"bluesky_{p.Id}",
                Title = $"{p.AuthorName ?? p.Author}: {TruncateText(p.Content, 100)}",
                Snippet = TruncateText(p.Content, 200),
                Source = "BlueSky",
                SourceId = p.Id,
                Url = $"https://bsky.app/profile/{p.Author}/post/{p.Id}",
                PublishedAt = p.TimestampUtc,
                Author = $"@{p.Author}",
                EngagementCount = 0, // BlueSky engagement data not stored in model
                CommentCount = CountReplies(p.Replies)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching BlueSky for query: {Query}", request.Query);
            return new List<OmniSearchResult>();
        }
    }

    /// <summary>
    /// Recursively count all replies in a reply tree
    /// </summary>
    private int CountReplies(List<TwitterFeedbackItem>? replies)
    {
        if (replies == null || !replies.Any())
            return 0;

        int count = replies.Count;
        foreach (var reply in replies)
        {
            count += CountReplies(reply.Replies);
        }
        return count;
    }

    /// <summary>
    /// Recursively count all replies in a reply tree (BlueSky)
    /// </summary>
    private int CountReplies(List<BlueSkyFeedbackItem>? replies)
    {
        if (replies == null || !replies.Any())
            return 0;

        int count = replies.Count;
        foreach (var reply in replies)
        {
            count += CountReplies(reply.Replies);
        }
        return count;
    }

    private List<OmniSearchResult> RankResults(List<OmniSearchResult> results)
    {
        // Ranking algorithm: prioritize comment count for analysis value
        // Score = (comment_count * 0.5) + (engagement * 0.3) + (recency_score * 0.2)
        var now = DateTimeOffset.UtcNow;

        return results.Select(r =>
        {
            var daysSincePublished = (now - r.PublishedAt).TotalDays;
            var recencyScore = Math.Max(0, 100 - daysSincePublished); // Newer = higher score
            var normalizedEngagement = Math.Log10(Math.Max(1, r.EngagementCount)); // Log scale
            var normalizedComments = Math.Log10(Math.Max(1, r.CommentCount + 1)); // Log scale, +1 to handle 0
            var combinedScore = (normalizedComments * 0.5) + (normalizedEngagement * 0.3) + (recencyScore * 0.2);
            
            return (Result: r, Score: combinedScore);
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Result)
        .ToList();
    }

    private string GenerateCacheKey(OmniSearchRequest request)
    {
        var keyString = $"omni:{request.Query}:{string.Join(",", request.Platforms.OrderBy(p => p))}:" +
                       $"{request.FromDate?.ToUnixTimeSeconds()}:{request.ToDate?.ToUnixTimeSeconds()}:" +
                       $"{request.Page}:{request.MaxResults}:{request.SortMode}";
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        return Convert.ToBase64String(hashBytes);
    }

    private string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Clear expired cache entries
    /// </summary>
    public void ClearExpiredCache()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => now - kvp.Value.CachedAt >= _cacheTTL)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        _logger.LogInformation("Cleared {Count} expired cache entries", expiredKeys.Count);
    }
}
