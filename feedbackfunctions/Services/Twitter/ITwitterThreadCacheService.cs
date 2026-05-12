using SharedDump.Models.TwitterFeedback;

namespace FeedbackFunctions.Services.Twitter;

public interface ITwitterThreadCacheService
{
    Task<TwitterThreadCacheResult> GetThreadAsync(
        string tweetUrlOrId,
        Func<Task<TwitterFeedbackResponse?>> fetchThreadAsync,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public readonly record struct TwitterThreadCacheResult(
    TwitterFeedbackResponse? Response,
    bool CacheHit,
    string CacheKey);
