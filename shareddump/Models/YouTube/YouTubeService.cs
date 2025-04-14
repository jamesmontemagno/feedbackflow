using System.Net.Http.Json;
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
}