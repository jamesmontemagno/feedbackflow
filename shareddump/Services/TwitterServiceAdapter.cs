using SharedDump.Models.TwitterFeedback;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

/// <summary>
/// Adapter for TwitterFeedbackFetcher to implement ITwitterService interface
/// </summary>
public class TwitterServiceAdapter : ITwitterService
{
    private readonly TwitterFeedbackFetcher _twitterFetcher;

    public TwitterServiceAdapter(TwitterFeedbackFetcher twitterFetcher)
    {
        _twitterFetcher = twitterFetcher;
    }

    public async Task<TwitterFeedbackResponse?> GetTwitterThreadAsync(string tweetUrlOrId)
    {
        return await _twitterFetcher.FetchFeedbackAsync(tweetUrlOrId);
    }
    
    public async Task<List<TwitterFeedbackItem>> SearchTweetsAsync(string query, int maxResults = 25, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
    {
        return await _twitterFetcher.SearchTweetsAsync(query, maxResults, fromDate, toDate, cancellationToken);
    }
}
