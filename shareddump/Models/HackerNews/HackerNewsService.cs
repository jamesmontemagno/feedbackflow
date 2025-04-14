using System.Net.Http.Json;
using System.Threading.Channels;
using SharedDump.Json;

namespace SharedDump.Models.HackerNews;

public class HackerNewsService
{
    private readonly HttpClient _client;

    public HackerNewsService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
    }

    public async Task<HackerNewsItem?> GetItemData(int itemId) =>
        await _client.GetFromJsonAsync($"https://hacker-news.firebaseio.com/v0/item/{itemId}.json", HackerNewsJsonContext.Default.HackerNewsItem);

    public IAsyncEnumerable<HackerNewsItem> GetItemWithComments(int itemId)
    {
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

        return commentsChannel.Reader.ReadAllAsync();
    }

    private async Task GetComments(int itemId, ChannelWriter<HackerNewsItem> writer)
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