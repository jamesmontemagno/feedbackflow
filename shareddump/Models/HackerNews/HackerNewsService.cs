using System.Net.Http.Json;
using System.Threading.Channels;
using SharedDump.Json;
using System.Text.Json;

namespace SharedDump.Models.HackerNews;

public class HackerNewsService
{
    private readonly HttpClient _client;

    public HackerNewsService(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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
                var story = await GetItemData(itemId);
                if (story != null)
                {
                    story.MainStoryId = itemId;
                    await commentsChannel.Writer.WriteAsync(story);
                    await GetComments(itemId, commentsChannel.Writer, itemId);
                }
                commentsChannel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                commentsChannel.Writer.TryComplete(ex);
            }
        });

        return commentsChannel.Reader.ReadAllAsync();
    }

    private async Task GetComments(int itemId, ChannelWriter<HackerNewsItem> writer, int mainStoryId)
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
                    commentData.MainStoryId = mainStoryId;
                    await writer.WriteAsync(commentData);
                }

                tasks.Add(GetComments(kidId, writer, mainStoryId));
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

    public async Task<List<HackerNewsItemBasicInfo>> SearchByTitleBasicInfo()
    {
        var topStories = await GetTopStories();
        var results = new List<HackerNewsItemBasicInfo>();

        var tasks = topStories
            .Select((storyId, index) => new { storyId, index })
            .Select(async x =>
            {
                var item = await GetItemData(x.storyId);
                if (!string.IsNullOrWhiteSpace(item?.Title))
                {
                    return new
                    {
                        Index = x.index,
                        Info = new HackerNewsItemBasicInfo
                        {
                            Id = item.Id,
                            Title = item.Title,
                            By = item.By ?? string.Empty,
                            Time = item.Time,
                            Url = item.Url,
                            Score = item.Score ?? 0,
                            Descendants = item.Descendants ?? 0
                        }
                    };
                }
                return null;
            });

        var items = await Task.WhenAll(tasks);

        results.AddRange(
            items
            .Where(i => i?.Info is not null)
            .OrderBy(i => i!.Index)
            .Select(i => i!.Info!)
        );

        return results;
    }
}