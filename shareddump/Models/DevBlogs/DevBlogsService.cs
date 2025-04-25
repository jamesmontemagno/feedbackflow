#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;
using Microsoft.Extensions.Logging;
using System.Net;

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

        var baseUrl = articleUrl.TrimEnd('/');
        var allComments = new List<XElement>();
        var pageNum = 1;
        XElement? firstChannel = null;

        while (true)
        {
            var feedUrl = baseUrl + "/feed" + (pageNum > 1 ? $"?paged={pageNum}" : "");
            try
            {
                var xml = await _client.GetStringAsync(feedUrl);
                var doc = XDocument.Parse(xml);
                var channel = doc.Root?.Element("channel");
                if (channel == null)
                    break;

                // Store first channel for article metadata
                if (firstChannel == null)
                {
                    firstChannel = channel;
                }

                var items = channel.Elements("item").ToList();
                if (!items.Any())
                    break;

                // Check for duplicates based on guid/link before adding
                var newItems = items.Where(item => 
                {
                    var itemGuid = item.Element("guid")?.Value?.Trim() ?? item.Element("link")?.Value?.Trim();
                    return !allComments.Any(existing => 
                        (existing.Element("guid")?.Value?.Trim() ?? existing.Element("link")?.Value?.Trim()) == itemGuid);
                }).ToList();
                
                if (!newItems.Any())
                    break;

                allComments.AddRange(newItems);
                pageNum++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized 
                                             || ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Stop when we hit a 401/404 - no more pages
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching page {PageNum} of DevBlogs RSS feed for {Url}", pageNum, articleUrl);
                break;
            }
        }

        if (firstChannel == null)
            return null;

        var articleTitle = firstChannel.Element("title")?.Value?.Trim();
        var articleLink = firstChannel.Element("link")?.Value?.Trim();
        var comments = new List<DevBlogsCommentModel>();
        var commentDict = new Dictionary<string, DevBlogsCommentModel>();

        // Process all comments from all pages
        foreach (var item in allComments)
        {
            var guidOrLink = item.Element("guid")?.Value?.Trim() ?? item.Element("link")?.Value?.Trim();
            var id = guidOrLink != null 
                ? System.Text.RegularExpressions.Regex.Match(guidOrLink, @"#comment-(\d+)").Groups[1].Value 
                : Guid.NewGuid().ToString();

            var author = item.Element(XName.Get("creator", "http://purl.org/dc/elements/1.1/"))?.Value?.Trim();
            var pubDateStr = item.Element("pubDate")?.Value?.Trim();
            DateTimeOffset? published = null;
            if (DateTimeOffset.TryParse(pubDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                published = dt;

            var bodyHtml = item.Element(XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/"))?.Value?.Trim();
            var contentEncoded = item.Element(XName.Get("encoded", "http://purl.org/rss/1.0/modules/content/"))?.Value;
            string? parentId = null;

            if (!string.IsNullOrEmpty(contentEncoded))
            {
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
            Comments = comments.Where(c => string.IsNullOrEmpty(c.ParentId))
                     .OrderByDescending(c => c.PublishedUtc)
                     .ToList()
        };
    }
}
