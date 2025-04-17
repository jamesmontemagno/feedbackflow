using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharedDump.Models.Reddit;
using SharedDump.Json;

var threadId = new Option<string>(
    ["--thread", "-t"],
    "The Reddit thread ID to dump comments from. Can be extracted from the URL.")
{
    IsRequired = true
};

var clientId = new Option<string?>(
    ["--client-id", "-c"],
    "The Reddit API client ID. If not specified, will look in environment variables or user secrets.");

var clientSecret = new Option<string?>(
    ["--client-secret", "-s"],
    "The Reddit API client secret. If not specified, will look in environment variables or user secrets.");

var outputDirectory = new Option<string?>(
    ["--output", "-o"],
    () => null,
    "The directory where the results will be written. Defaults to the current directory.");

var rootCommand = new RootCommand("CLI tool for extracting Reddit thread comments in JSON format.")
{
    threadId,
    clientId,
    clientSecret,
    outputDirectory
};

rootCommand.SetHandler(RunAsync, threadId, clientId, clientSecret, outputDirectory);

await rootCommand.InvokeAsync(args);

async Task<int> RunAsync(string threadId, string? clientId, string? clientSecret, string? outputDirectory)
{
    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>()
        .Build();

    // Check if we have credentials in user secrets
    var configClientId = config["Reddit:ClientId"];
    var configClientSecret = config["Reddit:ClientSecret"];

    try
    {
        clientId ??= configClientId;
        clientSecret ??= configClientSecret;

        if (string.IsNullOrEmpty(clientId))
        {
            Console.WriteLine("Reddit client ID not found in configuration.");
            return 1;
        }

        if (string.IsNullOrEmpty(clientSecret))
        {
            Console.WriteLine("Reddit client secret not found in configuration.");
            return 1;
        }

        outputDirectory ??= Environment.CurrentDirectory;

        var redditService = new RedditService(clientId, clientSecret);
        Console.WriteLine($"Processing Reddit thread: {threadId}");

        var thread = await redditService.GetThreadWithComments(threadId);
        var outputPath = Path.Combine(outputDirectory, $"reddit-{threadId}.json");

        await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(thread, RedditJsonContext.Default.RedditThreadModel));

        Console.WriteLine($"Output written to: {outputPath}");
        Console.WriteLine($"Found {thread.Comments.Length} comments");

        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}