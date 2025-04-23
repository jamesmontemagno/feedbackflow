using System.Net.Http.Json;
using System.Threading.Channels;
using SharedDump.Json;
using System.Text.Json;

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

    private async Task<int[]> GetTopStoriesIds()
    {
        var response = await _client.GetAsync("https://hacker-news.firebaseio.com/v0/topstories.json");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<int[]>(content) ?? Array.Empty<int>();
    }

    public async Task<int[]> GetTopStories()
    {
        return await _client.GetFromJsonAsync<int[]>(
            "https://hacker-news.firebaseio.com/v0/topstories.json",
            HackerNewsJsonContext.Default.Int32Array) ?? Array.Empty<int>();
    }

    public async Task<List<HackerNewsItemBasicInfo>> SearchByTitleBasicInfo(IEnumerable<string> keywords)
    {
        var topStories = await GetTopStories();
        var results = new List<HackerNewsItemBasicInfo>();

        foreach (var storyId in topStories)
        {
            var item = await GetItemData(storyId);
            if (item?.Title != null && (keywords.Count() == 0 || keywords.Any(k => 
                item.Title.Contains(k, StringComparison.OrdinalIgnoreCase))))
            {
                results.Add(new HackerNewsItemBasicInfo
                {
                    Id = item.Id,
                    Title = item.Title,
                    By = item.By ?? string.Empty,
                    Time = item.Time,
                    Url = item.Url,
                    Score = item.Score ?? 0,
                    Descendants = item.Descendants ?? 0
                });
            }
        }

        return results;
    }
}