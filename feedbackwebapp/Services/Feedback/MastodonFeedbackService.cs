using System.Net.Http;
using System.Threading.Tasks;
using FeedbackWebApp.Services.Authentication;
using Microsoft.Extensions.Configuration;
using SharedDump.Models;
using System.Text.Json;

namespace FeedbackWebApp.Services.Feedback;

public interface IMastodonFeedbackService : IFeedbackService
{
    Task<List<CommentThread>> SearchAsync(string query, string instance);
    Task<CommentThread?> GetThreadAsync(string statusUrl, string instance);
}

public class MastodonFeedbackService : IMastodonFeedbackService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationHeaderService _authHeaderService;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public MastodonFeedbackService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAuthenticationHeaderService authHeaderService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _authHeaderService = authHeaderService;
        _baseUrl = configuration["FeedbackApi:BaseUrl"] ?? "http://localhost:7071";
        _functionsKey = configuration["FeedbackApi:FunctionsKey"] ?? string.Empty;
    }

    public async Task<List<CommentThread>> SearchAsync(string query, string instance)
    {
        var url = $"{_baseUrl}/api/mastodon/search?q={Uri.EscapeDataString(query)}&instance={Uri.EscapeDataString(instance)}&code={Uri.EscapeDataString(_functionsKey)}";
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        await _authHeaderService.AddAuthenticationHeadersAsync(request);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<CommentThread>>(json) ?? new();
    }

    public async Task<CommentThread?> GetThreadAsync(string statusUrl, string instance)
    {
        var url = $"{_baseUrl}/api/mastodon/thread?url={Uri.EscapeDataString(statusUrl)}&instance={Uri.EscapeDataString(instance)}&code={Uri.EscapeDataString(_functionsKey)}";
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        await _authHeaderService.AddAuthenticationHeadersAsync(request);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CommentThread>(json);
    }
}
