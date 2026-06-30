using System.Net;
using System.Text.Json;
using SharedDump.Models.Reports;
using FeedbackWebApp.Services.Authentication;

namespace FeedbackWebApp.Services;

/// <summary>
/// Communicates with the Azure Functions backend to list generated reports and download
/// their raw data for the admin report-data portal.
///
/// API Endpoints:
/// - GET /api/GetAllReports                  - List all generated reports (metadata)
/// - GET /api/DownloadReportRawData?id={id}  - Download raw JSON data for a report
/// </summary>
public class ReportDataAdminService : IReportDataAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationHeaderService _headerService;
    private readonly ILogger<ReportDataAdminService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private readonly string _functionsKey;

    public ReportDataAdminService(
        HttpClient httpClient,
        IAuthenticationHeaderService headerService,
        ILogger<ReportDataAdminService> logger,
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

    public async Task<List<ReportDataListItem>> GetAllReportsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/api/GetAllReports?code={Uri.EscapeDataString(_functionsKey)}");
            await _headerService.AddAuthenticationHeadersAsync(request);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReportDataListResponse>(content, _jsonOptions);
                return result?.Reports ?? new List<ReportDataListItem>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCodeMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required",
                HttpStatusCode.Forbidden => "Admin access required",
                _ => $"Server error ({response.StatusCode})"
            };

            _logger.LogError("Failed to get all reports. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to get reports: {statusCodeMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all reports");
            throw;
        }
    }

    public async Task<string?> DownloadRawDataAsync(Guid reportId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_baseUrl}/api/DownloadReportRawData?id={Uri.EscapeDataString(reportId.ToString())}&code={Uri.EscapeDataString(_functionsKey)}");
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
            _logger.LogError("Failed to download raw data for report {ReportId}. Status: {StatusCode}, Error: {Error}",
                reportId, response.StatusCode, errorContent);
            throw new InvalidOperationException($"Failed to download raw data ({response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading raw data for report {ReportId}", reportId);
            throw;
        }
    }
}
