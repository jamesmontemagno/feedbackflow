using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FeedbackFlow.MCP.Shared;

[McpServerToolType]
public sealed class FeedbackFlowToolsShared
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthenticationProvider _authenticationProvider;
    private const string BaseUrl = "https://api.feedbackflow.app";

    public FeedbackFlowToolsShared(IHttpClientFactory httpClientFactory, IAuthenticationProvider authenticationProvider)
    {
        _httpClientFactory = httpClientFactory;
        _authenticationProvider = authenticationProvider;
    }

    public enum AnalysisType
    {
        AnalysisOnly = 0,
        CommentsOnly = 1,
        AnalysisAndComments = 2
    }

    private sealed class DeveloperReportRequest
    {
        public List<string> Urls { get; set; } = new();

        public string? CustomPrompt { get; set; }

        public bool SaveReport { get; set; }

        public bool IsPublic { get; set; } = true;
    }

    [McpServerTool, Description("Analyze feedback from various sources using AI (AutoAnalyze function).")]
    public async Task<string> AutoAnalyzeFeedback(
        [Description("The URL to analyze (GitHub, YouTube, Reddit, etc.)")] string url,
        [Description("Custom analysis prompt (optional)")] string? customPrompt = null,
        [Description("Output mode (optional): AnalysisOnly=0 (markdown), CommentsOnly=1 (JSON), AnalysisAndComments=2 (combined JSON)")] AnalysisType? type = null)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var queryParams = $"url={Uri.EscapeDataString(url)}";
            if (!string.IsNullOrEmpty(customPrompt))
                queryParams += $"&customPrompt={Uri.EscapeDataString(customPrompt)}";
            if (type.HasValue)
                queryParams += $"&type={(int)type.Value}";

            var requestUrl = $"{BaseUrl}/api/AutoAnalyze?{queryParams}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("x-api-key", apiKey);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }
            else
            {
                return $"Error: API call failed with status {response.StatusCode}. Response: {content}";
            }
        }
        catch (Exception ex)
        {
            return $"Error: Exception occurred - {ex.Message}";
        }
    }

    [McpServerTool, Description("Generate a combined AI analysis report from one or more feedback URLs, optionally save it publicly, and return the report URL.")]
    public async Task<string> GenerateDeveloperReport(
        [Description("The URLs to analyze and combine into a single report. Supported platforms include GitHub, YouTube, Reddit, DevBlogs, Twitter/X, BlueSky, and Hacker News.")] List<string> urls,
        [Description("Custom analysis prompt (optional)")] string? customPrompt = null,
        [Description("Save the generated report and return a web URL (default: true)")] bool saveReport = true,
        [Description("Make the saved report publicly accessible (default: true)")] bool isPublic = true)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        if (urls == null || urls.Count == 0 || urls.All(string.IsNullOrWhiteSpace))
        {
            return "Error: At least one URL is required.";
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            var requestBody = new DeveloperReportRequest
            {
                Urls = urls.Where(url => !string.IsNullOrWhiteSpace(url)).Select(url => url.Trim()).ToList(),
                CustomPrompt = customPrompt,
                SaveReport = saveReport,
                IsPublic = isPublic
            };

            var requestUrl = $"{BaseUrl}/api/GenerateDeveloperReport";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", apiKey);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }

            return $"Error: API call failed with status {response.StatusCode}. Response: {content}";
        }
        catch (Exception ex)
        {
            return $"Error: Exception occurred - {ex.Message}";
        }
    }

    [McpServerTool, Description("Generate Reddit subreddit analysis report.")]
    public async Task<string> RedditReport(
        [Description("The subreddit name to analyze")] string subreddit,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var queryParams = $"subreddit={Uri.EscapeDataString(subreddit)}&force={force}";
            var requestUrl = $"{BaseUrl}/api/RedditReport?{queryParams}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("x-api-key", apiKey);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }
            else
            {
                return $"Error: API call failed with status {response.StatusCode}. Response: {content}";
            }
        }
        catch (Exception ex)
        {
            return $"Error: Exception occurred - {ex.Message}";
        }
    }

    [McpServerTool, Description("Generate GitHub repository issues analysis report.")]
    public async Task<string> GitHubIssuesReport(
        [Description("The GitHub repository in format 'owner/repo'")] string repo,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var queryParams = $"repo={Uri.EscapeDataString(repo)}&force={force}";
            var requestUrl = $"{BaseUrl}/api/GitHubIssuesReport?{queryParams}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("x-api-key", apiKey);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }
            else
            {
                return $"Error: API call failed with status {response.StatusCode}. Response: {content}";
            }
        }
        catch (Exception ex)
        {
            return $"Error: Exception occurred - {ex.Message}";
        }
    }

    [McpServerTool, Description("Generate Reddit subreddit summary report.")]
    public async Task<string> RedditReportSummary(
        [Description("The subreddit name to summarize")] string subreddit,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var queryParams = $"subreddit={Uri.EscapeDataString(subreddit)}&force={force}";
            var requestUrl = $"{BaseUrl}/api/RedditReportSummary?{queryParams}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("x-api-key", apiKey);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }
            else
            {
                return $"Error: API call failed with status {response.StatusCode}. Response: {content}";
            }
        }
        catch (Exception ex)
        {
            return $"Error: Exception occurred - {ex.Message}";
        }
    }
}
