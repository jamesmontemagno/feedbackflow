using SharedDump.Models.Mastodon.ApiModels;
using SharedDump.Models;
using SharedDump.Services;
using Microsoft.Extensions.Logging;

namespace SharedDump.Services;

public interface IMastodonService
{
    Task<List<CommentThread>> SearchAsync(string query, string instance, CancellationToken cancellationToken = default);
    Task<CommentThread?> GetThreadAsync(string statusUrl, string instance, CancellationToken cancellationToken = default);
}

public class MastodonServiceAdapter : IMastodonService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MastodonServiceAdapter> _logger;

    public MastodonServiceAdapter(HttpClient httpClient, ILogger<MastodonServiceAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<CommentThread>> SearchAsync(string query, string instance, CancellationToken cancellationToken = default)
    {
        // Mastodon does not support global search across all instances.
        // We'll use the instance's public timeline and filter client-side.
        var url = $"https://{instance}/api/v2/search?q={Uri.EscapeDataString(query)}&type=statuses&resolve=true&limit=10";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var statuses = doc.RootElement.GetProperty("statuses");
        var threads = new List<CommentThread>();
        foreach (var statusElem in statuses.EnumerateArray())
        {
            var status = System.Text.Json.JsonSerializer.Deserialize<MastodonStatus>(statusElem.GetRawText(), MastodonFeedbackJsonContext.Default.MastodonStatus);
            if (status is not null)
            {
                threads.Add(ConvertToCommentThread(status, instance));
            }
        }
        return threads;
    }

    public async Task<CommentThread?> GetThreadAsync(string statusUrl, string instance, CancellationToken cancellationToken = default)
    {
        // Extract status ID from URL
        var id = ExtractStatusId(statusUrl);
        if (id is null)
            return null;
        var statusApiUrl = $"https://{instance}/api/v1/statuses/{id}";
        var contextApiUrl = $"https://{instance}/api/v1/statuses/{id}/context";
        var statusResp = await _httpClient.GetAsync(statusApiUrl, cancellationToken);
        statusResp.EnsureSuccessStatusCode();
        var status = await statusResp.Content.ReadFromJsonAsync<MastodonStatus>(MastodonFeedbackJsonContext.Default.MastodonStatus, cancellationToken: cancellationToken);
        var contextResp = await _httpClient.GetAsync(contextApiUrl, cancellationToken);
        contextResp.EnsureSuccessStatusCode();
        var context = await contextResp.Content.ReadFromJsonAsync<MastodonContext>(MastodonFeedbackJsonContext.Default.MastodonContext, cancellationToken: cancellationToken);
        if (status is null || context is null)
            return null;
        var thread = ConvertToCommentThread(status, instance, context);
        return thread;
    }

    private static string? ExtractStatusId(string url)
    {
        // Example: https://mastodon.social/@user/123456789012345678
        var parts = url.TrimEnd('/').Split('/');
        return parts.LastOrDefault();
    }

    private static CommentThread ConvertToCommentThread(MastodonStatus status, string instance, MastodonContext? context = null)
    {
        var thread = new CommentThread
        {
            Id = status.Id,
            Platform = "Mastodon",
            Title = status.Account.DisplayName ?? status.Account.Username,
            Url = status.Url ?? $"https://{instance}/@{status.Account.Username}/{status.Id}",
            Comments = new List<CommentData>(),
            Metadata = new Dictionary<string, object?>
            {
                ["instance"] = instance,
                ["author"] = status.Account.Username,
                ["created_at"] = status.CreatedAt,
            }
        };
        // Add root post as first comment
        thread.Comments.Add(ConvertToCommentData(status));
        // Add replies if context provided
        if (context is not null)
        {
            foreach (var reply in context.Descendants)
            {
                thread.Comments.Add(ConvertToCommentData(reply));
            }
        }
        return thread;
    }

    private static CommentData ConvertToCommentData(MastodonStatus status)
    {
        return new CommentData
        {
            Id = status.Id,
            Author = status.Account.DisplayName ?? status.Account.Username,
            Content = status.Content,
            CreatedAt = status.CreatedAt,
            Metadata = new Dictionary<string, object?>
            {
                ["username"] = status.Account.Username,
                ["avatar"] = status.Account.Avatar,
            }
        };
    }
}
