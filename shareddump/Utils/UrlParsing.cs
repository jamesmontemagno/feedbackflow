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
}