using System;
using System.CommandLine;
using System.Text.Json;
using SharedDump.Models.HackerNews;
using SharedDump.Json;
using SharedDump.Utils;

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

async Task<int> RunAsync(int[] itemIds, string? outputPath)
{
    using var httpClient = new HttpClient();
    var hnService = new HackerNewsService(httpClient);
    var processingTasks = new List<Task>(itemIds.Length);
    
    foreach (var itemId in itemIds)
    {
        processingTasks.Add(ProcessItem(itemId));
    }

    await Task.WhenAll(processingTasks);
    return 0;

    async Task ProcessItem(int itemId)
    {
        var item = await hnService.GetItemData(itemId);

        if (item?.Title is null)
        {
            Console.WriteLine($"Item {itemId} not found.");
            return;
        }

        Console.WriteLine($"Getting comments for \"{item.Title}\"");

        var fileName = $"{StringUtilities.Slugify(item.Title)}.comments.json";
        var fullPath = Path.Combine(outputPath ?? Environment.CurrentDirectory, fileName);

        var comments = hnService.GetItemWithComments(itemId);
        var count = 0;

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Write, 16 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(fileStream, comments, HackerNewsJsonContext.Default.IAsyncEnumerableHackerNewsItem);

        await foreach (var _ in comments)
        {
            count++;
        }

        Console.WriteLine($"Processed {count} comments.");
        Console.WriteLine($"Comments written to {fullPath}");
    }
}