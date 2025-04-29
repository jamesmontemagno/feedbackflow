using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharedDump.Models.YouTube;
using SharedDump.Json;

var userInput = new Option<FileInfo?>(["--input", "-i"], "The input file with video/playlist IDs.");
var channelId = new Option<string?>(["--channel", "-c"], "The ID of the channel.");
var outputPath = new Option<string?>(["--output", "-o"], () => null, "The path where the results will be written. Defaults to current directory.");

var rootCommand = new RootCommand("CLI tool for extracting YouTube comments in JSON format.")
{
    userInput,
    channelId,
    outputPath
};

rootCommand.SetHandler(RunAsync, userInput, channelId, outputPath);

await rootCommand.InvokeAsync(args);

static async Task<int> RunAsync(FileInfo? input, string? channelId, string? outputPath)
{
    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>()
        .Build();

    var apiKey = config["YouTube:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("YouTube API key not found in configuration.");
        return 1;
    }

    using var httpClient = new HttpClient();
    var youtubeService = new YouTubeService(apiKey, httpClient);
    var videoIds = new List<string>();

    if (input is not null)
    {
        if (!input.Exists)
        {
            Console.WriteLine($"Input file {input.FullName} does not exist.");
            return 1;
        }

        using var stream = input.OpenRead();
        var inputConfig = await JsonSerializer.DeserializeAsync(stream, YouTubeJsonContext.Default.YouTubeInputFile);

        if (inputConfig?.Videos is { Length: > 0 } inputIds)
        {
            videoIds.AddRange(inputIds);
        }

        if (inputConfig?.Playlists is { Length: > 0 } playlists)
        {
            foreach (var playlistId in playlists)
            {
                var playlistVideoIds = await youtubeService.GetPlaylistVideos(playlistId);
                videoIds.AddRange(playlistVideoIds);
            }
        }
    }
    else if (channelId is not null)
    {
        var channelVideoIds = await youtubeService.GetChannelVideos(channelId);
        videoIds.AddRange(channelVideoIds);
    }
    else
    {
        Console.WriteLine("Either an input file or a channel ID must be provided.");
        return 1;
    }

    var outputVideos = new List<YouTubeOutputVideo>();

    foreach (var videoId in videoIds)
    {
        var video = await youtubeService.ProcessVideo(videoId);
        outputVideos.Add(video);
    }

    var outputDirectory = outputPath ?? Environment.CurrentDirectory;
    var outputFile = Path.Combine(outputDirectory, "output.json");

    await using var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(fileStream, outputVideos.ToArray(), YouTubeJsonContext.Default.YouTubeOutputVideoArray);

    Console.WriteLine($"Output written to {outputFile}");

    return 0;
}
