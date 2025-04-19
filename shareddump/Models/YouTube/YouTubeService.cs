using System.Net.Http.Json;
using System.Text.Json;
using SharedDump.Json;

namespace SharedDump.Models.YouTube;

public class YouTubeService
{
    private readonly HttpClient _client;
    private readonly string _apiKey;

    public YouTubeService(string apiKey, HttpClient? client = null)
    {
        _apiKey = apiKey;
        _client = client ?? new HttpClient();
    }

    public async Task<IEnumerable<string>> GetPlaylistVideos(string playlistId)
    {
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"https://youtube.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={playlistId}&key={_apiKey}";
            if (pageToken is not null)
            {
                url += $"&pageToken={pageToken}";
            }

            try
            {
                var response = await _client.GetFromJsonAsync<YouTubeVideoResponse>(url, YouTubeJsonContext.Default.YouTubeVideoResponse);

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

    public async Task<IEnumerable<string>> GetChannelVideos(string channelId)
    {
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var url = $"https://youtube.googleapis.com/youtube/v3/search?part=snippet&maxResults=50&type=video&channelId={channelId}&key={_apiKey}";
            if (pageToken is not null)
            {
                url += $"&pageToken={pageToken}";
            }

            try
            {
                var response = await _client.GetFromJsonAsync<YouTubeVideoResponse>(url, YouTubeJsonContext.Default.YouTubeVideoResponse);

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

    public async Task<YouTubeOutputVideo> ProcessVideo(string videoId)
    {
        var video = new YouTubeOutputVideo
        {
            Id = videoId,
            Url = $"https://www.youtube.com/watch?v={videoId}"
        };

        try
        {
            var videoUrl = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet&id={videoId}&key={_apiKey}";
            var videoResponse = await _client.GetFromJsonAsync<YouTubeVideoResponse>(videoUrl, YouTubeJsonContext.Default.YouTubeVideoResponse);

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
                var commentsUrl = $"https://youtube.googleapis.com/youtube/v3/commentThreads?part=snippet,replies&maxResults=100&videoId={videoId}&key={_apiKey}";
                if (nextPageToken is not null)
                {
                    commentsUrl += $"&pageToken={nextPageToken}";
                }

                var commentResponse = await _client.GetFromJsonAsync<YouTubeCommentResponse>(commentsUrl, YouTubeJsonContext.Default.YouTubeCommentResponse);

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

    public async Task<List<YouTubeOutputVideo>> SearchVideos(string topic, string tag, DateTimeOffset cutoffDate)
    {
        var videoIds = new List<string>();
        string? pageToken = null;

        do
        {
            var queryParams = new List<string>
            {
                $"part=snippet",
                $"maxResults=50",
                $"type=video",
                $"key={_apiKey}",
                $"publishedAfter={cutoffDate.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}"
            };

            if (!string.IsNullOrEmpty(topic))
            {
                queryParams.Add($"q={Uri.EscapeDataString(topic)}");
            }

            if (!string.IsNullOrEmpty(tag))
            {
                queryParams.Add($"videoCategoryId={Uri.EscapeDataString(tag)}");
            }

            if (pageToken is not null)
            {
                queryParams.Add($"pageToken={pageToken}");
            }

            var url = $"https://youtube.googleapis.com/youtube/v3/search?{string.Join("&", queryParams)}";

            try
            {
                var response = await _client.GetFromJsonAsync<YouTubeVideoResponse>(url, YouTubeJsonContext.Default.YouTubeVideoResponse);

                if (response is null)
                {
                    break;
                }

                foreach (var item in response.Items)
                {
                    if (item.ContentDetails?.VideoId is { } videoId)
                    {
                        videoIds.Add(videoId);
                    }
                }

                pageToken = response.NextPageToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred searching videos: {ex.Message}");
                break;
            }
        } while (!string.IsNullOrEmpty(pageToken));

        var videos = new List<YouTubeOutputVideo>();
        foreach (var videoId in videoIds)
        {
            var video = await ProcessVideo(videoId);
            videos.Add(video);
        }

        return videos;
    }

    public async Task<List<YouTubeOutputVideo>> SearchVideosBasicInfo(string searchQuery, string tag, DateTimeOffset publishedAfter)
    {
        var videos = new List<YouTubeOutputVideo>();
        var pageToken = "";
        
        while (true)
        {
            var searchUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=50&q={Uri.EscapeDataString(searchQuery)}";
            if (!string.IsNullOrEmpty(tag))
            {
                searchUrl += $"&topicId={Uri.EscapeDataString(tag)}";
            }
            searchUrl += $"&publishedAfter={publishedAfter:yyyy-MM-dd}T00:00:00Z";
            if (!string.IsNullOrEmpty(pageToken))
            {
                searchUrl += $"&pageToken={pageToken}";
            }
            searchUrl += $"&key={_apiKey}";

            var searchResponse = await _client.GetFromJsonAsync<JsonElement>(searchUrl);
            if (searchResponse.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var snippet = item.GetProperty("snippet");
                    var videoId = item.GetProperty("id").GetProperty("videoId").GetString();
                    
                    if (videoId == null) continue;

                    // Get just basic video stats without comments
                    var videoUrl = $"https://www.googleapis.com/youtube/v3/videos?part=statistics&id={videoId}&key={_apiKey}";
                    var videoResponse = await _client.GetFromJsonAsync<JsonElement>(videoUrl);
                    
                    if (!videoResponse.TryGetProperty("items", out var videoItems) || 
                        !videoItems.EnumerateArray().Any()) continue;

                    var statistics = videoItems[0].GetProperty("statistics");
                    var publishedAt = DateTimeOffset.Parse(snippet.GetProperty("publishedAt").GetString() ?? string.Empty);

                    videos.Add(new YouTubeOutputVideo
                    {
                        Id = videoId,
                        Title = snippet.GetProperty("title").GetString() ?? string.Empty,
                        Description = snippet.GetProperty("description").GetString() ?? string.Empty,
                        PublishedAt = publishedAt,
                        UploadDate = publishedAt.DateTime,
                        ChannelId = snippet.GetProperty("channelId").GetString() ?? string.Empty,
                        ChannelTitle = snippet.GetProperty("channelTitle").GetString() ?? string.Empty,
                        Url = $"https://www.youtube.com/watch?v={videoId}",
                        ViewCount = ParseStatistic(statistics, "viewCount"),
                        LikeCount = ParseStatistic(statistics, "likeCount"),
                        CommentCount = ParseStatistic(statistics, "commentCount")
                    });
                }
            }

            if (!searchResponse.TryGetProperty("nextPageToken", out var nextPageToken))
                break;

            pageToken = nextPageToken.GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(pageToken))
                break;
        }

        return videos;
    }

    private static long ParseStatistic(JsonElement statistics, string propertyName)
    {
        return statistics.TryGetProperty(propertyName, out var property) && 
               property.TryGetInt64(out var value) ? value : 0;
    }
}