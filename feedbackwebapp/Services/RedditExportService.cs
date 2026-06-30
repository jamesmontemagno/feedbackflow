using System.Net;
using System.Text;
using System.Text.Json;
using SharedDump.Models.Reports;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

/// <summary>
/// Communicates with the Azure Functions backend to create, list, download, and delete
/// on-demand Reddit subreddit exports for the admin export portal.
///
/// API Endpoints:
/// - POST   /api/CreateRedditExport            - Create a new export
/// - GET    /api/GetRedditExports              - List all stored exports (metadata)
/// - GET    /api/DownloadRedditExport?id={id}  - Download full JSON for an export
/// - DELETE /api/DeleteRedditExport?id={id}    - Delete an export
/// </summary>
public class RedditExportService : IRedditExportService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<RedditExportService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public RedditExportService(
        HttpClient httpClient,
        IAuthenticationHeaderService headerService,
        ILogger<RedditExportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _headerService = headerService;
        _logger = logger;
        _baseUrl = configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("FeedbackApi:BaseUrl not configured");
        _functionsKey = configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("FeedbackApi:FunctionsKey not configured");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<RedditExportListItem> CreateExportAsync(CreateRedditExportRequest request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/api/CreateRedditExport?code={Uri.EscapeDataString(_functionsKey)}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json")
            };
            await _headerService.AddAuthenticationHeadersAsync(httpRequest);

            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<RedditExportListItem>(content, _jsonOptions);
                return result ?? throw new InvalidOperationException("Empty response from export service.");
            }

            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required",
                HttpStatusCode.Forbidden => "Admin access required",
                HttpStatusCode.BadRequest => string.IsNullOrWhiteSpace(content)
                    ? "Invalid export request"
                    : content,
                _ => $"Server error ({response.StatusCode})"
            };

            _logger.LogError("Failed to create export. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, content);
            throw new InvalidOperationException(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Reddit export");
            throw;
        }
    }

    public async Task<List<RedditExportListItem>> GetExportsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/api/GetRedditExports?code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RedditExportListResponse>(content, _jsonOptions);
                return result?.Exports ?? new List<RedditExportListItem>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCodeMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required",
                HttpStatusCode.Forbidden => "Admin access required",
                _ => $"Server error ({response.StatusCode})"
            };

            _logger.LogError("Failed to get exports. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to get exports: {statusCodeMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Reddit exports");
            throw;
        }
    }

    public async Task<string?> DownloadExportAsync(Guid exportId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/api/DownloadRedditExport?id={Uri.EscapeDataString(exportId.ToString())}&code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to download export {ExportId}. Status: {StatusCode}, Error: {Error}",
                exportId, response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to download export ({response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading Reddit export {ExportId}", exportId);
            throw;
        }
    }

    public async Task<bool> DeleteExportAsync(Guid exportId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{_baseUrl}/api/DeleteRedditExport?id={Uri.EscapeDataString(exportId.ToString())}&code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete export {ExportId}. Status: {StatusCode}, Error: {Error}",
                exportId, response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to delete export ({response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Reddit export {ExportId}", exportId);
            throw;
        }
    }
}
