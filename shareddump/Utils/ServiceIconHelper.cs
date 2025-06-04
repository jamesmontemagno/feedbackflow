namespace SharedDump.Utils;

public static class ServiceIconHelper
{
    /// <summary>
    /// Gets the Bootstrap icon class for a given service type
    /// </summary>
    /// <param name="sourceType">The service type (YouTube, GitHub, etc.)</param>
    /// <returns>The Bootstrap icon class name</returns>
    public static string GetServiceIcon(string? sourceType)
    {
        return sourceType?.ToLowerInvariant() switch
        {
            "youtube" => "bi-youtube",
            "github" => "bi-github", 
            "reddit" => "bi-reddit",
            "twitter" => "bi-twitter",
            "bluesky" => "bi-cloud",
            "hackernews" => "bi-braces",
            "devblogs" => "bi-journal-code",
            "manual" => "bi-pencil-square",
            _ => "bi-question-circle"
        };
    }
}
