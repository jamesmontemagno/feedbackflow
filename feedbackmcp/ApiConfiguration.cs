namespace FeedbackMCP;

public class ApiConfiguration
{
    public required string GitHubAccessToken { get; init; }
    public required string YouTubeApiKey { get; init; }
    public required string RedditClientId { get; init; }
    public required string RedditClientSecret { get; init; }
}