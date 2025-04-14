using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
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

async Task<int> RunAsync(FileInfo? input, string? channelId, string? outputPath)
{
    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>()
        .Build();

    var apiKey = config["YouTube:ApiKey"];

    using var client = new HttpClient();

    var videoIds = new List<string>();

    if (input is not null)
    {
        if (!input.Exists)
        {
            Console.WriteLine($"Input file {input.FullName} does not exist.");
            return 1;
        }

        using var stream = input.OpenRead();
        var inputConfig = await JsonSerializer.DeserializeAsync(stream, YouTubeApiJsonContext.Default.YouTubeInputFile);

        if (inputConfig?.Videos is { Length: > 0 } inputIds)
        {
            videoIds.AddRange(inputIds);
        }

        if (inputConfig?.Playlists is { Length: > 0 } playlists)
        {
            foreach (var playlistId in playlists)
            {
                var playlistVideoIds = await GetPlaylistVideos(playlistId);
                videoIds.AddRange(playlistVideoIds);
            }
        }
    }
    else if (channelId is not null)
    {
        var channelVideoIds = await GetChannelVideos(channelId);
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
        var video = await ProcessVideo(videoId);
        outputVideos.Add(video);
    }

    var outputDirectory = outputPath ?? Environment.CurrentDirectory;
    var outputFile = Path.Combine(outputDirectory, "output.json");

    await using var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(fileStream, outputVideos.ToArray(), YouTubeJsonContext.Default.YouTubeOutputVideoArray);

    Console.WriteLine($"Output written to {outputFile}");

    return 0;

    async Task<IEnumerable<string>> GetPlaylistVideos(string playlistId)
    {
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"https://youtube.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={playlistId}&key={apiKey}";
            if (pageToken is not null)
            {
                url += $"&pageToken={pageToken}";
            }

            try
            {
                var response = await client.GetFromJsonAsync<YouTubeVideoResponse>(url, YouTubeApiJsonContext.Default.YouTubeVideoResponse);

                if (response is null)
                {
                    break;
                }

                foreach (var item in response.Items)
                {
                    if (item.Snippet.ResourceId?.VideoId is { } videoId)
                    {
                        videoIds.Add(videoId);
                    }
                }

                pageToken = response.NextPageToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                break;
            }

        } while (!string.IsNullOrEmpty(pageToken));

        return videoIds;
    }

    async Task<IEnumerable<string>> GetChannelVideos(string channelId)
    {
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=50&type=video&channelId={channelId}&key={apiKey}";
            if (pageToken is not null)
            {
                url += $"&pageToken={pageToken}";
            }

            try
            {
                var response = await client.GetFromJsonAsync<YouTubeVideoResponse>(url, YouTubeApiJsonContext.Default.YouTubeVideoResponse);

                if (response is null)
                {
                    break;
                }

                foreach (var item in response.Items)
                {
                    if (item.Snippet.ResourceId?.VideoId is { } videoId)
                    {
                        videoIds.Add(videoId);
                    }
                }

                pageToken = response.NextPageToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                break;
            }
        } while (!string.IsNullOrEmpty(pageToken));

        return videoIds;
    }

    async Task<YouTubeOutputVideo> ProcessVideo(string videoId)
    {
        var video = new YouTubeOutputVideo
        {
            Id = videoId,
            Url = $"https://www.youtube.com/watch?v={videoId}"
        };

        try
        {
            var videoUrl = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet&id={videoId}&key={apiKey}";
            var videoResponse = await client.GetFromJsonAsync<YouTubeVideoResponse>(videoUrl, YouTubeApiJsonContext.Default.YouTubeVideoResponse);

            if (videoResponse?.Items.Count > 0)
            {
                var videoInfo = videoResponse.Items[0];
                video.Title = videoInfo.Snippet.Title;
                video.UploadDate = videoInfo.Snippet.PublishedAt;
            }

            var commentsList = new List<YouTubeOutputComment>();
            string? nextPageToken = null;

            do
            {
                var commentsUrl = $"https://youtube.googleapis.com/youtube/v3/commentThreads?part=snippet,replies&maxResults=100&videoId={videoId}&key={apiKey}";
                if (nextPageToken is not null)
                {
                    commentsUrl += $"&pageToken={nextPageToken}";
                }

                var commentResponse = await client.GetFromJsonAsync<YouTubeCommentResponse>(commentsUrl, YouTubeApiJsonContext.Default.YouTubeCommentResponse);

                if (commentResponse?.Items is null)
                {
                    break;
                }

                foreach (var item in commentResponse.Items)
                {
                    var comment = item.Snippet.TopLevelComment;
                    commentsList.Add(new YouTubeOutputComment
                    {
                        Id = comment.Id,
                        Author = comment.Snippet.AuthorDisplayName,
                        Text = comment.Snippet.TextDisplay,
                        PublishedAt = comment.Snippet.PublishedAt
                    });

                    if (item.Replies.Comments.Count > 0)
                    {
                        foreach (var reply in item.Replies.Comments)
                        {
                            commentsList.Add(new YouTubeOutputComment
                            {
                                Id = reply.Id,
                                Author = reply.Snippet.AuthorDisplayName,
                                Text = reply.Snippet.TextDisplay,
                                PublishedAt = reply.Snippet.PublishedAt,
                                ParentId = item.Id
                            });
                        }
                    }
                }

                nextPageToken = commentResponse.NextPageToken;

            } while (!string.IsNullOrEmpty(nextPageToken));

            video.Comments = commentsList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return video;
    }
}
