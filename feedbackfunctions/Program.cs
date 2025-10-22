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
using FeedbackFunctions.Services.Authentication;
using FeedbackFunctions.Middleware;
using FeedbackFunctions.Services.Account;
using FeedbackFunctions.Services.Email;
using System.Configuration;
using Azure.Storage.Blobs;
using FeedbackFunctions.Services.Reports;
using FeedbackFunctions.OmniSearch;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Note: Usage validation will be handled via attributes on individual functions
// rather than global middleware due to complexity in current Functions runtime
builder.Configuration.AddUserSecrets<Program>(true);


var throwIfNullOrEmpty = false;
#if DEBUG
var useMocks = builder.Configuration.GetValue<bool>("UseMocks");
#else
var useMocks = false; // In production, we don't use mocks
throwIfNullOrEmpty = true;
#endif



// Register HTTP client factory
builder.Services.AddHttpClient();

// Register authentication services
builder.Services.AddScoped<IAuthUserTableService, AuthUserTableService>();
builder.Services.AddScoped<FeedbackFunctions.Middleware.AuthenticationMiddleware>();

// Register unified account service
RegisterAccountServices(builder.Services);

// Register blob storage and cache services
builder.Services.AddSingleton<IReportCacheService>(serviceProvider =>
{
    var configuration = GetConfig(serviceProvider);
    var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReportCacheService>>();
    var storageConnection = configuration["ProductionStorage"] ?? throw new InvalidOperationException("Production storage connection string not configured");
    var serviceClient = new BlobServiceClient(storageConnection);
    var containerClient = serviceClient.GetBlobContainerClient("reports");
    containerClient.CreateIfNotExists();
    return new ReportCacheService(logger, containerClient);
});

// Register admin report config service
builder.Services.AddScoped<IAdminReportConfigService, AdminReportConfigService>();

// Register OmniSearch service
builder.Services.AddScoped<OmniSearchService>();

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
    builder.Services.AddScoped<IEmailService, MockEmailService>();
    
    // Register unified account service
    RegisterAccountServices(builder.Services);
}
else
{
    builder.Services.AddScoped<IGitHubService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var githubToken = configuration["GitHub:AccessToken"];
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            Console.WriteLine("Using mock data for GitHub, no access token provided.");
            return new MockGitHubService();
        }
        return new GitHubService(githubToken, httpClientFactory.CreateClient("GitHub"));
    });
    
    builder.Services.AddScoped<IYouTubeService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var ytApiKey = configuration["YouTube:ApiKey"];
        if (string.IsNullOrWhiteSpace(ytApiKey))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("YouTube API key is not configured.");

            Console.WriteLine("Using mock data for YouTube, no API key provided.");
            return new MockYouTubeService();
        }
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
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("Reddit client ID and secret are not configured.");
            Console.WriteLine("Using mock data for Reddit, no client ID or secret provided.");
            return new MockRedditService();
        }
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
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("Azure OpenAI configuration is incomplete.");
            Console.WriteLine("Using mock data for Feedback Analyzer, no Azure OpenAI configuration provided.");
            return new MockFeedbackAnalyzerService();
        }
        return new FeedbackAnalyzerService(endpoint, apiKey, deployment);
    });
    
    builder.Services.AddScoped<ITwitterService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var twitterBearerToken = configuration["Twitter:BearerToken"];
        if (string.IsNullOrWhiteSpace(twitterBearerToken))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("Twitter bearer token is not configured.");

            Console.WriteLine("Using mock data for Twitter, no bearer token provided.");
            return new MockTwitterService();
        }
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
        if (string.IsNullOrWhiteSpace(blueSkyUsername) || string.IsNullOrWhiteSpace(blueSkyAppPassword))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("BlueSky username and app password are not configured.");
                
            Console.WriteLine("Using mock data for BlueSky, no username or app password provided.");
            return new MockBlueSkyService();
        }
        var blueSkyFetcher = new BlueSkyFeedbackFetcher(httpClientFactory.CreateClient("BlueSky"), Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueSkyFeedbackFetcher>.Instance);
        blueSkyFetcher.SetCredentials(blueSkyUsername, blueSkyAppPassword);
        return new BlueSkyServiceAdapter(blueSkyFetcher);
    });
    
    // Register email service
    builder.Services.AddScoped<IEmailService>(serviceProvider =>
    {
        var configuration = GetConfig(serviceProvider);
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmailService>>();
        
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (throwIfNullOrEmpty)
                throw new ConfigurationErrorsException("Azure Communication Services connection string is not configured.");
                
            Console.WriteLine("Using mock email service, no Azure Communication Services connection string provided.");
            var mockLogger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MockEmailService>>();
            return new MockEmailService(mockLogger);
        }
        
        return new EmailService(configuration, logger);
    });
    
    // Register unified account service
    RegisterAccountServices(builder.Services);
}

IConfiguration GetConfig(IServiceProvider? serviceProvider = null)
{
#if DEBUG
    return serviceProvider?.GetRequiredService<IConfiguration>() ?? builder.Configuration;
#else
    return serviceProvider?.GetRequiredService<IConfiguration>() ?? throw new InvalidOperationException("Configuration service not available.");
#endif
}

void RegisterAccountServices(IServiceCollection services)
{
    // Register the unified user account service
    services.AddSingleton<IUserAccountService>(sp =>
    {
        var config = GetConfig(sp);
        var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<UserAccountService>>();
        var storage = config["ProductionStorage"] ?? "UseDevelopmentStorage=true";
        return new UserAccountService(storage, config, logger);
    });
    
    // Register the API key service
    services.AddScoped<IApiKeyService, ApiKeyService>();
}

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var app = builder.Build();

// Ensure tables exist on startup
using (var scope = app.Services.CreateScope())
{
    var userAccountService = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
    await userAccountService.InitializeTablesAsync();
}

app.Run();
