using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedDump.Models.ContentSearch;
using FeedbackWebApp.Services.Authentication;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.ContentFeed;

/// <summary>
/// Client service for calling the OmniSearch Azure Function
/// </summary>
public class OmniSearchClientService : IOmniSearchService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<OmniSearchClientService> _logger;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public OmniSearchClientService(
        HttpClient httpClient,
        IAuthenticationHeaderService headerService,
        ILogger<OmniSearchClientService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _headerService = headerService;
        _logger = logger;
        _baseUrl = configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("FeedbackApi:BaseUrl not configured");
        _functionsKey = configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("FeedbackApi:FunctionsKey not configured");
    }

    public async Task<OmniSearchResponse> SearchAsync(OmniSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Executing omni-search for query: {Query} across platforms: {Platforms}", 
                request.Query, string.Join(", ", request.Platforms));

            var url = $"{_baseUrl}/api/OmniSearch?code={Uri.EscapeDataString(_functionsKey)}";

            // Use POST for complex requests
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json")
            };

            await _headerService.AddAuthenticationHeadersAsync(httpRequest);

            var httpResponse = await _httpClient.SendAsync(httpRequest);
            httpResponse.EnsureSuccessStatusCode();

            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OmniSearchResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null)
            {
                throw new InvalidOperationException("Failed to deserialize omni-search response");
            }

            _logger.LogInformation("Omni-search completed successfully. Found {Count} results", result.TotalCount);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during omni-search");
            throw new InvalidOperationException($"Failed to execute omni-search: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing omni-search");
            throw;
        }
    }
}

/// <summary>
/// Mock implementation of omni-search service for development
/// </summary>
public class MockOmniSearchService : IOmniSearchService
{
    private readonly ILogger<MockOmniSearchService> _logger;

    public MockOmniSearchService(ILogger<MockOmniSearchService> logger)
    {
        _logger = logger;
    }

    public async Task<OmniSearchResponse> SearchAsync(OmniSearchRequest request)
    {
        await Task.Delay(500); // Simulate network delay

        _logger.LogInformation("Mock: Returning sample omni-search results for query: {Query}", request.Query);

        var mockResults = new List<OmniSearchResult>();

        // Generate mock results for each platform
        if (request.Platforms.Contains("youtube", StringComparer.OrdinalIgnoreCase))
        {
            mockResults.AddRange(GenerateMockYouTubeResults(request.Query));
        }

        if (request.Platforms.Contains("reddit", StringComparer.OrdinalIgnoreCase))
        {
            mockResults.AddRange(GenerateMockRedditResults(request.Query));
        }

        if (request.Platforms.Contains("hackernews", StringComparer.OrdinalIgnoreCase))
        {
            mockResults.AddRange(GenerateMockHackerNewsResults(request.Query));
        }

        // Sort and paginate
        mockResults = request.SortMode.ToLowerInvariant() == "ranked"
            ? mockResults.OrderByDescending(r => r.EngagementCount).ToList()
            : mockResults.OrderByDescending(r => r.PublishedAt).ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Min(request.MaxResults, 50);
        var totalCount = mockResults.Count;
        var pagedResults = mockResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new OmniSearchResponse
        {
            Results = pagedResults,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            CachedAt = DateTimeOffset.UtcNow,
            Query = request.Query,
            PlatformsSearched = request.Platforms
        };
    }

    private List<OmniSearchResult> GenerateMockYouTubeResults(string query)
    {
        return new List<OmniSearchResult>
        {
            new()
            {
                Id = "youtube_mock1",
                Title = $"Ultimate {query} Tutorial - Complete Guide",
                Snippet = $"Learn everything about {query} in this comprehensive tutorial. Perfect for beginners and advanced users.",
                Source = "YouTube",
                SourceId = "mock_vid_1",
                Url = "https://www.youtube.com/watch?v=mock1",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-2),
                Author = "Tech Channel",
                EngagementCount = 125000
            },
            new()
            {
                Id = "youtube_mock2",
                Title = $"Top 10 {query} Tips and Tricks",
                Snippet = $"Discover the best practices and hidden features of {query} that will boost your productivity.",
                Source = "YouTube",
                SourceId = "mock_vid_2",
                Url = "https://www.youtube.com/watch?v=mock2",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-5),
                Author = "Dev Tips",
                EngagementCount = 85000
            }
        };
    }

    private List<OmniSearchResult> GenerateMockRedditResults(string query)
    {
        return new List<OmniSearchResult>
        {
            new()
            {
                Id = "reddit_mock1",
                Title = $"Discussion: Best practices for {query}",
                Snippet = $"Let's discuss the best approaches to working with {query}. Share your experiences and learnings.",
                Source = "Reddit",
                SourceId = "mock_post_1",
                Url = "https://reddit.com/r/programming/comments/mock1",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Author = "u/developer123",
                EngagementCount = 342
            },
            new()
            {
                Id = "reddit_mock2",
                Title = $"Just released: New {query} features!",
                Snippet = $"Excited to share the latest updates to {query}. Check out what's new!",
                Source = "Reddit",
                SourceId = "mock_post_2",
                Url = "https://reddit.com/r/webdev/comments/mock2",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-3),
                Author = "u/techwriter",
                EngagementCount = 189
            }
        };
    }

    private List<OmniSearchResult> GenerateMockHackerNewsResults(string query)
    {
        return new List<OmniSearchResult>
        {
            new()
            {
                Id = "hn_mock1",
                Title = $"{query}: The Future of Development",
                Snippet = $"An in-depth analysis of where {query} is heading and what it means for developers.",
                Source = "HackerNews",
                SourceId = "12345678",
                Url = "https://news.ycombinator.com/item?id=12345678",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Author = "techwriter",
                EngagementCount = 456
            }
        };
    }
}
