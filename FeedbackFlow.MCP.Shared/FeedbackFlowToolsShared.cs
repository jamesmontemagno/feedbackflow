using System.ComponentModel;
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

    private async Task<string> ExecuteApiRequestAsync(string endpoint, string queryParams)
    {
        var apiKey = _authenticationProvider.GetAuthenticationToken();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return _authenticationProvider.GetAuthenticationErrorMessage();
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var requestUrl = $"{BaseUrl}/api/{endpoint}?{queryParams}";
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

    [McpServerTool, Description("Analyze feedback from various sources using AI (AutoAnalyze function).")]
    public async Task<string> AutoAnalyzeFeedback(
        [Description("The URL to analyze (GitHub, YouTube, Reddit, etc.)")] string url,
        [Description("Maximum number of comments to analyze (default: 1000)")] int maxComments = 1000,
        [Description("Custom analysis prompt (optional)")] string? customPrompt = null,
        [Description("Output mode (optional): AnalysisOnly=0 (markdown), CommentsOnly=1 (JSON), AnalysisAndComments=2 (combined JSON)")] AnalysisType? type = null)
    {
        var queryParams = $"url={Uri.EscapeDataString(url)}&maxComments={maxComments}";
        if (!string.IsNullOrEmpty(customPrompt))
            queryParams += $"&customPrompt={Uri.EscapeDataString(customPrompt)}";
        if (type.HasValue)
            queryParams += $"&type={(int)type.Value}";

        return await ExecuteApiRequestAsync("AutoAnalyze", queryParams);
    }

    [McpServerTool, Description("Get raw comments from feedback sources without AI analysis (returns JSON).")]
    public async Task<string> GetComments(
        [Description("The URL to get comments from (GitHub, YouTube, Reddit, etc.)")] string url,
        [Description("Maximum number of comments to retrieve (default: 1000)")] int maxComments = 1000)
    {
        var queryParams = $"url={Uri.EscapeDataString(url)}&maxComments={maxComments}&type={(int)AnalysisType.CommentsOnly}";
        return await ExecuteApiRequestAsync("AutoAnalyze", queryParams);
    }

    [McpServerTool, Description("Generate Reddit subreddit analysis report.")]
    public async Task<string> RedditReport(
        [Description("The subreddit name to analyze")] string subreddit,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var queryParams = $"subreddit={Uri.EscapeDataString(subreddit)}&force={force}";
        return await ExecuteApiRequestAsync("RedditReport", queryParams);
    }

    [McpServerTool, Description("Generate GitHub repository issues analysis report.")]
    public async Task<string> GitHubIssuesReport(
        [Description("The GitHub repository in format 'owner/repo'")] string repo,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var queryParams = $"repo={Uri.EscapeDataString(repo)}&force={force}";
        return await ExecuteApiRequestAsync("GitHubIssuesReport", queryParams);
    }

    [McpServerTool, Description("Generate Reddit subreddit summary report.")]
    public async Task<string> RedditReportSummary(
        [Description("The subreddit name to summarize")] string subreddit,
        [Description("Force regeneration of cached report (default: false)")] bool force = false)
    {
        var queryParams = $"subreddit={Uri.EscapeDataString(subreddit)}&force={force}";
        return await ExecuteApiRequestAsync("RedditReportSummary", queryParams);
    }
}