using SharedDump.Models.ContentSearch;

namespace FeedbackWebApp.Services.Interfaces;

/// <summary>
/// Service for executing omni-search across multiple platforms
/// </summary>
public interface IOmniSearchService
{
    /// <summary>
    /// Execute a search across multiple platforms
    /// </summary>
    /// <param name="request">Search request with query and platform filters</param>
    /// <returns>Aggregated search results</returns>
    Task<OmniSearchResponse> SearchAsync(OmniSearchRequest request);
}
