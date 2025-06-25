using SharedDump.Models.YouTube;

namespace SharedDump.Services.Interfaces;

public interface IYouTubeService
{
    Task<IEnumerable<string>> GetPlaylistVideos(string playlistId);
    Task<IEnumerable<string>> GetChannelVideos(string channelId);
    Task<YouTubeOutputVideo> ProcessVideo(string videoId);
    Task<List<YouTubeOutputVideo>> SearchVideos(string topic, string tag, DateTimeOffset cutoffDate);
    Task<List<YouTubeOutputVideo>> SearchVideosBasicInfo(string searchQuery, string tag, DateTimeOffset publishedAfter);
}
