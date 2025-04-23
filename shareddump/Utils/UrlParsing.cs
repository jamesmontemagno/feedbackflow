using System;
using System.Collections.Generic;
using System.Linq;

namespace SharedDump.Utils;

public static class UrlParsing
{
    public static string? ExtractVideoId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var items = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<string>();

        foreach (var item in items)
        {
            if (Uri.TryCreate(item, UriKind.Absolute, out var uri))
            {
                // Handle youtube.com/watch?v= format
                if (uri.Host.Contains("youtube.com") && uri.Query.Contains("v="))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var id = query["v"];
                    if (!string.IsNullOrEmpty(id)) results.Add(id);
                }
                else if (uri.Host.Contains("youtube.com") && uri.AbsolutePath.StartsWith("/live/"))
                {
                    var id = uri.AbsolutePath.Substring("/live/".Length);
                    if (!string.IsNullOrEmpty(id)) results.Add(id);
                }
                // Handle youtu.be/ format
                else if (uri.Host == "youtu.be")
                {
                    var id = uri.AbsolutePath.Trim('/');
                    if (!string.IsNullOrEmpty(id)) results.Add(id);
                }
            }
            else
            {
                // Treat as raw ID
                results.Add(item.Trim());
            }
        }

        return results.Count > 0 ? string.Join(",", results) : null;
    }

    public static string? ExtractPlaylistId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var items = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<string>();

        foreach (var item in items)
        {
            if (Uri.TryCreate(item, UriKind.Absolute, out var uri))
            {
                if (uri.Host.Contains("youtube.com"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var id = query["list"];
                    if (!string.IsNullOrEmpty(id)) results.Add(id);
                }
            }
            else
            {
                // Treat as raw ID
                results.Add(item.Trim());
            }
        }

        return results.Count > 0 ? string.Join(",", results) : null;
    }

    public static string? ExtractHackerNewsId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var items = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<string>();

        foreach (var item in items)
        {
            if (Uri.TryCreate(item, UriKind.Absolute, out var uri))
            {
                // Handle news.ycombinator.com/item?id= format
                if (uri.Host.Contains("ycombinator.com") && uri.AbsolutePath.Contains("/item"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var id = query["id"];
                    if (!string.IsNullOrEmpty(id)) results.Add(id);
                }
            }
            else
            {
                // Treat as raw ID
                if (long.TryParse(item.Trim(), out _))
                {
                    results.Add(item.Trim());
                }
            }
        }

        return results.Count > 0 ? string.Join(",", results) : null;
    }

    public static string ExtractRedditId(string[] urls)
    {
        if (urls == null || urls.Length == 0)
            return string.Empty;

        var processedIds = new List<string>();

        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
                continue;

            // Handle direct thread IDs
            if (url.StartsWith("t3_"))
            {
                processedIds.Add(url[3..]);
                continue;
            }

            // Handle full Reddit URLs
            if (TryParseRedditUrl(url, out var threadId))
            {
                processedIds.Add(threadId);
            }
            else if (IsValidRedditId(url))
            {
                processedIds.Add(url);
            }
        }

        return string.Join(",", processedIds);
    }

    private static bool TryParseRedditUrl(string url, out string threadId)
    {
        threadId = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var uri = new Uri(url);
            if (!uri.Host.Contains("reddit.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var segments = uri.Segments;
            var commentsIndex = Array.IndexOf(segments, "comments/");
            if (commentsIndex == -1 || commentsIndex + 1 >= segments.Length)
                return false;

            threadId = segments[commentsIndex + 1].Trim('/');
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidRedditId(string id)
    {
        // Reddit IDs are 5-7 characters of base36 (alphanumeric)
        return !string.IsNullOrWhiteSpace(id) && 
               id.Length >= 5 && 
               id.Length <= 7 && 
               id.All(c => char.IsLetterOrDigit(c));
    }
}