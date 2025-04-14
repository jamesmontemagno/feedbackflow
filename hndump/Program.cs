using System;
using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using SharedDump.Models.HackerNews;
using SharedDump.Json;

var ids = new Option<int[]>(["--id"], "The article id. Multiple can be specified.")
{
    IsRequired = true,
    AllowMultipleArgumentsPerToken = true
};

var outputPath = new Option<string?>(["-o", "--output"], () => null, "The directory where the results will be written. Defaults to the current working directory");

var rootCommand = new RootCommand("CLI tool for extracting HackerNews comments in JSON format.")
{
    ids,
    outputPath
};

rootCommand.SetHandler(RunAsync, ids, outputPath);

await rootCommand.InvokeAsync(args);

async Task RunAsync(int[] itemIds, string? outputPath)
{
    var client = new HttpClient();

    var processingTasks = new List<Task>(itemIds.Length);
    foreach (var itemId in itemIds)
    {
        processingTasks.Add(ProcessItem(itemId));
    }

    await Task.WhenAll(processingTasks);

    async Task ProcessItem(int itemId)
    {
        var item = await GetItemData(itemId);

        if (item?.Title is null)
        {
            Console.WriteLine($"Item {itemId} not found.");
            return;
        }

        Console.WriteLine($"Getting comments for \"{item.Title}\"");

        var commentsChannel = Channel.CreateUnbounded<HackerNewsItem>();

        _ = Task.Run(async () =>
        {
            try
            {
                await GetComments(itemId, commentsChannel.Writer);
                commentsChannel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                commentsChannel.Writer.TryComplete(ex);
            }
        });

        async IAsyncEnumerable<HackerNewsItem> GetAllComments()
        {
            var count = 0;

            // This is here just so that we can print out which item is being processed (and count them)
            await foreach (var comment in commentsChannel.Reader.ReadAllAsync())
            {
                count++;
                yield return comment;
            }

            Console.WriteLine($"Processed {count} comments.");
        }

        var fileName = $"{Slugify(item.Title)}.comments.json";
        var fullPath = Path.Combine(outputPath ?? Environment.CurrentDirectory, fileName);

        // Write comments to file using JsonSerializer and a FileStream
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Write, 16 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(fileStream, GetAllComments(), HackerNewsJsonContext.Default.IAsyncEnumerableHackerNewsItem);

        Console.WriteLine($"Comments written to {fullPath}");

        string Slugify(string? value) =>
            value is null ? "" : Regexes.SlugRegex().Replace(value, "-").ToLowerInvariant();

        Task<HackerNewsItem?> GetItemData(int itemId) =>
            client.GetFromJsonAsync($"https://hacker-news.firebaseio.com/v0/item/{itemId}.json", HackerNewsJsonContext.Default.HackerNewsItem);

        async Task GetComments(int itemId, ChannelWriter<HackerNewsItem> writer)
        {
            HackerNewsItem? itemData = await GetItemData(itemId);

            if (itemData?.Kids is { } kids)
            {
                var tasks = new List<Task>(kids.Count);

                foreach (var kidId in kids)
                {
                    var commentData = await GetItemData(kidId);

                    if (commentData is not null)
                    {
                        await writer.WriteAsync(commentData);
                    }

                    tasks.Add(GetComments(kidId, writer));
                }

                await Task.WhenAll(tasks);
            }
        }
    }
}

public partial class Regexes
{
    [GeneratedRegex(@"[^A-Za-z0-9_\.~]+")]
    public static partial Regex SlugRegex();
}