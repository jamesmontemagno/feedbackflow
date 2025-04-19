namespace FeedbackWebApp.Components.ContentFeed.Services;

public abstract class ContentFeedService
{
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;

    protected ContentFeedService(HttpClient http, IConfiguration configuration)
    {
        Http = http;
        Configuration = configuration;
        BaseUrl = configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    public abstract Task<object?> FetchContent();
}