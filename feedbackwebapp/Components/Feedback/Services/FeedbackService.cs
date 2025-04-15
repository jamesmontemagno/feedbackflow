using System.Text;
using System.Text.Json;

namespace FeedbackWebApp.Components.Feedback.Services;

public abstract class FeedbackService
{
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;

    protected FeedbackService(HttpClient http, IConfiguration configuration)
    {
        Http = http;
        Configuration = configuration;
        BaseUrl = configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
    }

    protected async Task<string> AnalyzeComments(string serviceType, string comments)
    {
        var analyzeCode = Configuration["FeedbackApi:AnalyzeCommentsCode"]
            ?? throw new InvalidOperationException("Analyze API code not configured");

        var analyzeRequestBody = JsonSerializer.Serialize(new
        {
            serviceType,
            comments
        });

        var analyzeContent = new StringContent(
            analyzeRequestBody, 
            Encoding.UTF8, 
            "application/json");

        var getAnalysisUrl = $"{BaseUrl}/api/AnalyzeComments?code={Uri.EscapeDataString(analyzeCode)}";
        var analyzeResponse = await Http.PostAsync(getAnalysisUrl, analyzeContent);

        analyzeResponse.EnsureSuccessStatusCode();
        return await analyzeResponse.Content.ReadAsStringAsync();
    }

    public abstract Task<(string markdownResult, object? additionalData)> GetFeedback();
}