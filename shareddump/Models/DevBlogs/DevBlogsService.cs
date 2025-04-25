#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SharedDump.Models.DevBlogs;

/// <summary>
/// Service for fetching and parsing DevBlogs article comments from RSS.
/// </summary>
public class DevBlogsService
{
    private readonly HttpClient _client;
    private readonly ILogger? _logger;

    public DevBlogsService(HttpClient? client = null, ILogger? logger = null)
    {
        _client = client ?? new HttpClient();
        _logger = logger;
    }

    /// <summary>
    /// Fetches and parses comments for a DevBlogs article.
    /// </summary>
    /// <param name="articleUrl">The URL of the DevBlogs article.</param>
    /// <returns>The article model with comments and replies.</returns>
    public async Task<DevBlogsArticleModel?> FetchArticleWithCommentsAsync(string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(articleUrl))
            throw new ArgumentException("Article URL is required", nameof(articleUrl));

        var feedUrl = articleUrl.TrimEnd('/') + "/feed";
        try
        {
            var xml = await _client.GetStringAsync(feedUrl);
            var doc = XDocument.Parse(xml);
            var channel = doc.Root?.Element("channel");
            if (channel == null)
                return null;

            var articleTitle = channel.Element("title")?.Value?.Trim();
            var articleLink = channel.Element("link")?.Value?.Trim();

            var items = channel.Elements("item").ToList();
            var comments = new List<DevBlogsCommentModel>();
            var commentDict = new Dictionary<string, DevBlogsCommentModel>();

            foreach (var item in items)
            {
                var id = item.Element("guid")?.Value?.Trim() ?? item.Element("link")?.Value?.Trim() ?? Guid.NewGuid().ToString();
                var author = item.Element(XName.Get("creator", "http://purl.org/dc/elements/1.1/"))?.Value?.Trim();
                var pubDateStr = item.Element("pubDate")?.Value?.Trim();
                DateTimeOffset? published = null;
                if (DateTimeOffset.TryParse(pubDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                    published = dt;                var bodyHtml = item.Element(XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/"))?.Value?.Trim()
                    ?? item.Element("description")?.Value?.Trim();                // Try to extract parent comment from content:encoded ("In reply to <a href=...#comment-xxxxx">...")
                var contentEncoded = item.Element(XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/"))?.Value;
                string? parentId = null;
                if (!string.IsNullOrEmpty(contentEncoded))
                {
                    // Use more robust regex pattern to match the full comment URL format
                    // Matches any devblogs.microsoft.com URL with a comment ID
                    var commentMatch = System.Text.RegularExpressions.Regex.Match(
                        contentEncoded, 
                        @"<a href=""https?://devblogs\.microsoft\.com/[^""]*?#comment-(\d+)""[^>]*>([^<]+)</a>");
                    if (commentMatch.Success && commentMatch.Groups.Count > 1)
                    {
                        parentId = commentMatch.Groups[1].Value;
                    }
                }

                var comment = new DevBlogsCommentModel
                {
                    Id = id,
                    Author = author,
                    BodyHtml = bodyHtml,
                    PublishedUtc = published,
                    ParentId = parentId
                };
                commentDict[id] = comment;
            }

            // Build comment tree
            foreach (var comment in commentDict.Values)
            {
                if (!string.IsNullOrEmpty(comment.ParentId) && commentDict.TryGetValue(comment.ParentId, out var parent))
                {
                    parent.Replies.Add(comment);
                }
                else
                {
                    comments.Add(comment);
                }
            }

            return new DevBlogsArticleModel
            {
                Title = articleTitle,
                Url = articleLink,
                Comments = comments
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch or parse DevBlogs RSS feed for {Url}", articleUrl);
            return null;
        }
    }
}
