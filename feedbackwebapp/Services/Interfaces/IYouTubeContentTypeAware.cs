using SharedDump.Models.YouTube;

namespace FeedbackWebApp.Services.Interfaces;

public interface IYouTubeContentTypeAware
{
    void SetContentType(YouTubeContentType contentType);
    YouTubeContentType GetContentType();
}
