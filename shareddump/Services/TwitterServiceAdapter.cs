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
}
