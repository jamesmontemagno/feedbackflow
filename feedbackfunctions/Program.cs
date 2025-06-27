using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedDump.AI;
using SharedDump.Models.BlueSkyFeedback;
using SharedDump.Models.DevBlogs;
using SharedDump.Models.GitHub;
using SharedDump.Models.HackerNews;
using SharedDump.Models.Reddit;
using SharedDump.Models.TwitterFeedback;
using SharedDump.Models.YouTube;
using SharedDump.Services;
using SharedDump.Services.Interfaces;
using SharedDump.Services.Mock;
using System.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.ConfigureFunctionsWebApplication();


#if DEBUG
var c = GetConfig(null);

// Check if we should use mock services
var useMocks = c.GetValue<bool>("UseMocks");
#else
var useMocks = false; // In production, we don't use mocks
#endif



// Register HTTP client factory
builder.Services.AddHttpClient();

// Register services based on UseMocks setting
if (useMocks)
{
    Console.WriteLine("Using mock data 🍕🍕🍕");
    builder.Services.AddScoped<IGitHubService, MockGitHubService>();
    builder.Services.AddScoped<IYouTubeService, MockYouTubeService>();
    builder.Services.AddScoped<IHackerNewsService, MockHackerNewsService>();
    builder.Services.AddScoped<IRedditService, MockRedditService>();
    builder.Services.AddScoped<IDevBlogsService, MockDevBlogsService>();
    builder.Services.AddScoped<IFeedbackAnalyzerService, MockFeedbackAnalyzerService>();
    builder.Services.AddScoped<ITwitterService, MockTwitterService>();
    builder.Services.AddScoped<IBlueSkyService, MockBlueSkyService>();
}
else
{
    builder.Services.AddScoped<IGitHubService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var githubToken = configuration["GitHub:AccessToken"];
        if(githubToken is null)
            return new MockGitHubService();
        return new GitHubService(githubToken, httpClientFactory.CreateClient("GitHub"));
    });
    
    builder.Services.AddScoped<IYouTubeService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var ytApiKey = configuration["YouTube:ApiKey"];
        if(ytApiKey is null)
            return new MockYouTubeService();
        return new YouTubeService(ytApiKey, httpClientFactory.CreateClient("YouTube"));
    });
    
    builder.Services.AddScoped<IHackerNewsService>(serviceProvider =>
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var hackerNewsService = new HackerNewsService(httpClientFactory.CreateClient("HackerNews"));
        return new HackerNewsServiceAdapter(hackerNewsService);
    });
    
    builder.Services.AddScoped<IRedditService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var clientId = configuration["Reddit:ClientId"];
        var clientSecret = configuration["Reddit:ClientSecret"];
        if(clientId is null || clientSecret is null)
            return new MockRedditService();
        var redditService = new RedditService(clientId, clientSecret, httpClientFactory.CreateClient("Reddit"));
        return new RedditServiceAdapter(redditService);
    });
    
    builder.Services.AddScoped<IDevBlogsService>(serviceProvider =>
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DevBlogsService>>();
        var devBlogsService = new DevBlogsService(httpClientFactory.CreateClient("DevBlogs"), logger);
        return new DevBlogsServiceAdapter(devBlogsService);
    });
    
    builder.Services.AddScoped<IFeedbackAnalyzerService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var endpoint = configuration["Azure:OpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = configuration["Azure:OpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI API key not configured");
        var deployment = configuration["Azure:OpenAI:Deployment"] ?? throw new InvalidOperationException("Azure OpenAI deployment name not configured");
        return new FeedbackAnalyzerService(endpoint, apiKey, deployment);
    });
    
    builder.Services.AddScoped<ITwitterService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var twitterBearerToken = configuration["Twitter:BearerToken"];
        if(twitterBearerToken is null)
            return new MockTwitterService();
        var twitterHttpClient = httpClientFactory.CreateClient("Twitter");
        twitterHttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", twitterBearerToken);
        var twitterFetcher = new TwitterFeedbackFetcher(twitterHttpClient, Microsoft.Extensions.Logging.Abstractions.NullLogger<TwitterFeedbackFetcher>.Instance);
        return new TwitterServiceAdapter(twitterFetcher);
    });
    
    builder.Services.AddScoped<IBlueSkyService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var blueSkyUsername = configuration["BlueSky:Username"];
        var blueSkyAppPassword = configuration["BlueSky:AppPassword"];
        if(blueSkyUsername is null || blueSkyAppPassword is null)
            return new MockBlueSkyService();
        var blueSkyFetcher = new BlueSkyFeedbackFetcher(httpClientFactory.CreateClient("BlueSky"), Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueSkyFeedbackFetcher>.Instance);
        blueSkyFetcher.SetCredentials(blueSkyUsername, blueSkyAppPassword);
        return new BlueSkyServiceAdapter(blueSkyFetcher);
    });
}

IConfiguration GetConfig(IServiceProvider? serviceProvider = null)
{
    return builder.Configuration;
// #if DEBUG
//     return new ConfigurationBuilder()
//                     .AddJsonFile("local.settings.json")
//                     .AddUserSecrets<Program>()
//                     .Build();
// #else
//     return serviceProvider?.GetRequiredService<IConfiguration>() ?? throw new InvalidOperationException("Configuration service not available.");
// #endif
}

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
