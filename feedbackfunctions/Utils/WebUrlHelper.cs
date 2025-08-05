using Microsoft.Extensions.Configuration;

namespace FeedbackFunctions.Utils;

/// <summary>
/// Helper class to provide the web application URL with fallback to default production URL
/// </summary>
public static class WebUrlHelper
{
    private const string DefaultWebUrl = "https://www.feedbackflow.app";
    
    /// <summary>
    /// Gets the web URL from configuration, falling back to the default production URL if not configured
    /// </summary>
    /// <param name="configuration">The configuration service</param>
    /// <returns>The web URL to use for generating links</returns>
    public static string GetWebUrl(IConfiguration configuration)
    {
        return configuration["WebUrl"] ?? DefaultWebUrl;
    }
    
    /// <summary>
    /// Builds a full URL to a specific page/endpoint
    /// </summary>
    /// <param name="configuration">The configuration service</param>
    /// <param name="path">The path to append to the web URL (should start with /)</param>
    /// <returns>The complete URL</returns>
    public static string BuildUrl(IConfiguration configuration, string path)
    {
        var baseUrl = GetWebUrl(configuration);
        
        // Ensure the base URL doesn't end with a slash and the path starts with one
        baseUrl = baseUrl.TrimEnd('/');
        if (!path.StartsWith('/'))
        {
            path = '/' + path;
        }
        
        return baseUrl + path;
    }
    
    /// <summary>
    /// Builds a URL for viewing a report by ID
    /// </summary>
    /// <param name="configuration">The configuration service</param>
    /// <param name="reportId">The report ID</param>
    /// <param name="source">Optional source parameter for tracking</param>
    /// <returns>The complete URL to view the report</returns>
    public static string BuildReportUrl(IConfiguration configuration, Guid reportId, string? source = null)
    {
        var url = BuildUrl(configuration, $"/report/{reportId}");
        
        if (!string.IsNullOrEmpty(source))
        {
            url += $"?source={source}";
        }
        
        return url;
    }
    
    /// <summary>
    /// Builds a URL for viewing a report with query parameters
    /// </summary>
    /// <param name="configuration">The configuration service</param>
    /// <param name="reportId">The report ID</param>
    /// <param name="source">Optional source parameter for tracking</param>
    /// <returns>The complete URL to view the report with query parameters</returns>
    public static string BuildReportQueryUrl(IConfiguration configuration, Guid reportId, string? source = null)
    {
        var baseUrl = GetWebUrl(configuration);
        var url = $"{baseUrl}/?id={reportId}";
        
        if (!string.IsNullOrEmpty(source))
        {
            url += $"&source={source}";
        }
        
        return url;
    }
}
