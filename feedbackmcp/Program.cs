using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FeedbackMCP;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>();

        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Configure API tokens
        var apiConfig = new ApiConfiguration
        {
            GitHubAccessToken = builder.Configuration["GitHub_ACCESS_TOKEN"] 
                ?? throw new InvalidOperationException("GitHub access token not configured"),
            YouTubeApiKey = builder.Configuration["YOUTUBE_API_KEY"] 
                ?? throw new InvalidOperationException("YouTube API key not configured"),
            RedditClientId = builder.Configuration["REDDIT_CLIENT_ID"]
                ?? throw new InvalidOperationException("Reddit client ID not configured"),
            RedditClientSecret = builder.Configuration["REDDIT_CLIENT_SECRET"]
                ?? throw new InvalidOperationException("Reddit client secret not configured")
        };

        // Register ApiConfiguration and FeedbackFlowTool as singletons
        builder.Services.AddSingleton(apiConfig);

        // Configure MCP server
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<FeedbackFlowTool>();

        var host = builder.Build();
        await host.RunAsync();
    }
}
