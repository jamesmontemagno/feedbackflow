using System.Net;
using SharedDump.Services;

namespace FeedbackWebApp.Services;

public interface IUrlValidationHttpService
{
    Task<bool> CheckUrlExistsAsync(string url);
    Task<(bool IsValid, string? ErrorMessage)> ValidateGitHubRepositoryAsync(string owner, string repo);
    Task<(bool IsValid, string? ErrorMessage)> ValidateRedditSubredditAsync(string subreddit);
}

public class UrlValidationHttpService : IUrlValidationHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UrlValidationHttpService> _logger;

    public UrlValidationHttpService(HttpClient httpClient, ILogger<UrlValidationHttpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Set user agent to avoid being blocked
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FeedbackFlow-WebApp/1.0");
    }

    public async Task<bool> CheckUrlExistsAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request);
            
            // Consider 2xx and 3xx status codes as "exists"
            // GitHub might return 429 for rate limiting, treat that as exists too
            return response.IsSuccessStatusCode || 
                   response.StatusCode == HttpStatusCode.Redirect ||
                   response.StatusCode == HttpStatusCode.MovedPermanently ||
                   response.StatusCode == HttpStatusCode.Found ||
                   response.StatusCode == HttpStatusCode.TooManyRequests;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking URL existence: {Url}", url);
            // If we can't check, assume it exists to avoid false negatives
            return true;
        }
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateGitHubRepositoryAsync(string owner, string repo)
    {
        // First validate the format using the shared service
        var ownerResult = UrlValidationService.ValidateGitHubOwnerName(owner);
        if (!ownerResult.IsValid)
            return (false, ownerResult.ErrorMessage);

        var repoResult = UrlValidationService.ValidateGitHubRepoName(repo);
        if (!repoResult.IsValid)
            return (false, repoResult.ErrorMessage);

        // Then check if the repository exists
        var githubUrl = UrlValidationService.ConstructGitHubUrl(owner, repo);
        var urlExists = await CheckUrlExistsAsync(githubUrl);
        
        if (!urlExists)
            return (false, $"GitHub repository '{owner}/{repo}' does not exist or is not accessible.");

        return (true, null);
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateRedditSubredditAsync(string subreddit)
    {
        // First validate the format using the shared service
        var subredditResult = UrlValidationService.ValidateSubredditName(subreddit);
        if (!subredditResult.IsValid)
            return (false, subredditResult.ErrorMessage);

        // Then check if the subreddit exists
        var redditUrl = UrlValidationService.ConstructRedditUrl(subreddit);
        var urlExists = await CheckUrlExistsAsync(redditUrl);
        
        if (!urlExists)
            return (false, $"Subreddit 'r/{subreddit}' does not exist or is not accessible.");

        return (true, null);
    }
}