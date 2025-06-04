using SharedDump.Utils;

namespace FeedbackWebApp.Utils;

/// <summary>
/// URL helper methods for the FeedbackWebApp project that delegate to SharedDump utilities
/// to ensure consistent behavior across projects.
/// </summary>
public static class UrlHelpers
{
    /// <summary>
    /// Checks if a string is a valid URL.
    /// Delegates to SharedDump.Utils.UrlParsing.IsValidUrl.
    /// </summary>
    public static bool IsValidUrl(string? input)
    {
        return UrlParsing.IsValidUrl(input);
    }
    
    /// <summary>
    /// Gets the YouTube thumbnail URL for a video ID.
    /// Delegates to SharedDump.Utils.UrlParsing.GetYouTubeThumbnailUrl.
    /// </summary>
    public static string GetYouTubeThumbnailUrl(string videoId)
    {
        return UrlParsing.GetYouTubeThumbnailUrl(videoId);
    }
    
    /// <summary>
    /// Gets the Bootstrap icon class for a given service type.
    /// Delegates to SharedDump.Utils.ServiceIconHelper.GetServiceIcon.
    /// </summary>
    public static string GetServiceIcon(string? sourceType)
    {
        return ServiceIconHelper.GetServiceIcon(sourceType);
    }
}
