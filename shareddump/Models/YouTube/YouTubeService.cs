using System.Net.Http.Json;
using System.Text.Json;
using SharedDump.Json;
using SharedDump.Services.Interfaces;

namespace SharedDump.Models.YouTube;

public class YouTubeService : IYouTubeService
{
    private readonly HttpClient _client;
    private readonly string _apiKey;

    public YouTubeService(string apiKey, HttpClient client)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _client = client ?? throw new ArgumentNullException(nameof(client));
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

            var searchResponse = await _client.GetFromJsonAsync<YouTubeSearchResponse>(searchUrl, YouTubeJsonContext.Default.YouTubeSearchResponse);

            if (searchResponse?.Items == null) break;

            // Collect all video IDs from this page
            var videoIds = searchResponse.Items.Select(item => item.Id.VideoId).ToList();

            if (videoIds.Any())
            {
                // Make a single batch request for all video statistics
                var videoUrl = $"https://www.googleapis.com/youtube/v3/videos?part=statistics&maxResults=50&id={string.Join(",", videoIds)}&key={_apiKey}";
                var statsResponse = await _client.GetFromJsonAsync<YouTubeVideoStatisticsResponse>(videoUrl, YouTubeJsonContext.Default.YouTubeVideoStatisticsResponse);

                if (statsResponse?.Items != null)
                {
                    // Create a lookup dictionary for quick access to statistics
                    var statsLookup = statsResponse.Items.ToDictionary(v => v.Id, v => v.Statistics);

                    // Process each search result with its corresponding statistics
                    foreach (var item in searchResponse.Items)
                    {
                        var videoId = item.Id.VideoId;
                        var snippet = item.Snippet;

                        if (statsLookup.TryGetValue(videoId, out var statistics))
                        {
                            var publishedAt = DateTimeOffset.Parse(snippet.PublishedAt);

                            videos.Add(new YouTubeOutputVideo
                            {
                                Id = videoId,
                                Title = snippet.Title,
                                Description = snippet.Description,
                                PublishedAt = publishedAt,
                                UploadDate = publishedAt.DateTime,
                                ChannelId = snippet.ChannelId,
                                ChannelTitle = snippet.ChannelTitle,
                                Url = $"https://www.youtube.com/watch?v={videoId}",
                                ViewCount = string.IsNullOrEmpty(statistics.ViewCount) ? 0 : long.Parse(statistics.ViewCount),
                                LikeCount = string.IsNullOrEmpty(statistics.LikeCount) ? 0 : long.Parse(statistics.LikeCount),
                                CommentCount = string.IsNullOrEmpty(statistics.CommentCount) ? 0 : long.Parse(statistics.CommentCount)
                            });
                        }
                    }
                }
            }

            pageToken = searchResponse.NextPageToken ?? string.Empty;
            if (string.IsNullOrEmpty(pageToken) || videos.Count > 50)
                break;
        }

        return videos;
    }

    private static long ParseStatistic(JsonElement statistics, string propertyName)
    {
        return statistics.TryGetProperty(propertyName, out var property) && 
               property.TryGetInt64(out var value) ? value : 0;
    }

    public async Task<YouTubeOutputVideo> ProcessVideo(string videoId, YouTubeContentType contentType)
    {
        var video = await ProcessVideo(videoId);
        
        // If we only need comments or already have them, return
        if (contentType == YouTubeContentType.Comments)
        {
            return video;
        }

        // Fetch transcript if needed
        if (contentType == YouTubeContentType.Transcript || contentType == YouTubeContentType.Both)
        {
            video.Transcript = await GetTranscript(videoId);
        }

        // Clear comments if only transcript is needed
        if (contentType == YouTubeContentType.Transcript)
        {
            video.Comments.Clear();
        }

        return video;
    }

    public async Task<YouTubeTranscript?> GetTranscript(string videoId, string? languageCode = null)
    {
        try
        {
            // First, get the video page to extract transcript information
            var videoPageUrl = $"https://www.youtube.com/watch?v={videoId}";
            var videoPageResponse = await _client.GetStringAsync(videoPageUrl);

            // Extract caption track information from the page
            // YouTube embeds caption track URLs in the page source
            var captionTracksMatch = System.Text.RegularExpressions.Regex.Match(
                videoPageResponse, 
                @"""captionTracks"":\s*(\[.*?\])"
            );

            if (!captionTracksMatch.Success)
            {
                // No captions available
                return null;
            }

            var captionTracksJson = captionTracksMatch.Groups[1].Value;
            var captionTracks = JsonSerializer.Deserialize<List<JsonElement>>(captionTracksJson);

            if (captionTracks is null || captionTracks.Count == 0)
            {
                return null;
            }

            // Find the best caption track (prefer specified language, fallback to English, then first available)
            JsonElement? selectedTrack = null;
            if (!string.IsNullOrEmpty(languageCode))
            {
                selectedTrack = captionTracks.FirstOrDefault(t => 
                    t.TryGetProperty("languageCode", out var lang) && 
                    lang.GetString() == languageCode);
            }

            // Fallback to English
            selectedTrack ??= captionTracks.FirstOrDefault(t => 
                t.TryGetProperty("languageCode", out var lang) && 
                lang.GetString() == "en");

            // Fallback to first available
            selectedTrack ??= captionTracks.FirstOrDefault();

            if (selectedTrack is null || !selectedTrack.Value.TryGetProperty("baseUrl", out var baseUrlProp))
            {
                return null;
            }

            var transcriptUrl = baseUrlProp.GetString();
            if (string.IsNullOrEmpty(transcriptUrl))
            {
                return null;
            }

            // Fetch the transcript XML
            var transcriptXml = await _client.GetStringAsync(transcriptUrl);

            // Parse the transcript XML
            var segments = new List<YouTubeTranscriptSegment>();
            var textMatches = System.Text.RegularExpressions.Regex.Matches(
                transcriptXml,
                @"<text start=""([\d.]+)"" dur=""([\d.]+)""[^>]*>(.*?)</text>"
            );

            foreach (System.Text.RegularExpressions.Match match in textMatches)
            {
                var startStr = match.Groups[1].Value;
                var durationStr = match.Groups[2].Value;
                var text = match.Groups[3].Value;

                // Decode HTML entities
                text = System.Net.WebUtility.HtmlDecode(text);

                if (double.TryParse(startStr, out var start) && 
                    double.TryParse(durationStr, out var duration))
                {
                    segments.Add(new YouTubeTranscriptSegment
                    {
                        Text = text,
                        Start = start,
                        Duration = duration
                    });
                }
            }

            if (segments.Count == 0)
            {
                return null;
            }

            var language = selectedTrack.Value.TryGetProperty("languageCode", out var langProp) 
                ? langProp.GetString() ?? "unknown" 
                : "unknown";

            return new YouTubeTranscript
            {
                VideoId = videoId,
                Language = language,
                Segments = segments
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching transcript for video {videoId}: {ex.Message}");
            return null;
        }
    }
}