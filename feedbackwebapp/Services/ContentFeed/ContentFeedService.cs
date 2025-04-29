namespace FeedbackWebApp.Services.ContentFeed;

public abstract class ContentFeedService
{
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;

    protected ContentFeedService(IHttpClientFactory http, IConfiguration configuration)
    {
        Http = http.CreateClient("DefaultClient");
        Configuration = configuration;
        BaseUrl = configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    public abstract Task<object?> FetchContent();
}