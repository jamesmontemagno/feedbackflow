using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

/// <summary>
/// Adapter for BlueSkyFeedbackFetcher to implement IBlueSkyService interface
/// </summary>
public class BlueSkyServiceAdapter : IBlueSkyService
{
    private readonly BlueSkyFeedbackFetcher _blueSkyFetcher;

    public BlueSkyServiceAdapter(BlueSkyFeedbackFetcher blueSkyFetcher)
    {
        _blueSkyFetcher = blueSkyFetcher;
    }

    public async Task<BlueSkyFeedbackResponse?> GetBlueSkyPostAsync(string postUrlOrId)
    {
        return await _blueSkyFetcher.FetchFeedbackAsync(postUrlOrId);
    }

    public void SetCredentials(string username, string appPassword)
    {
        _blueSkyFetcher.SetCredentials(username, appPassword);
    }
}
