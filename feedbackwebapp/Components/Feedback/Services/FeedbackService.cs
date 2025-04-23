using System.Text;
using System.Text.Json;

namespace FeedbackWebApp.Components.Feedback.Services;

public enum FeedbackProcessStatus
{
    GatheringComments,
    AnalyzingComments,
    Completed
}

public delegate void FeedbackStatusUpdate(FeedbackProcessStatus status, string message);

public abstract class FeedbackService
{
    public const int MaxCommentsToAnalyze = 1200;
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;
    protected readonly FeedbackStatusUpdate? OnStatusUpdate;

    protected FeedbackService(HttpClient http, IConfiguration configuration, FeedbackStatusUpdate? onStatusUpdate = null)
    {
        Http = http;
        Configuration = configuration;
        BaseUrl = configuration["FeedbackApi:BaseUrl"] 
            ?? throw new InvalidOperationException("API base URL not configured");
        OnStatusUpdate = onStatusUpdate;
    }

    protected void UpdateStatus(FeedbackProcessStatus status, string message)
    {
        OnStatusUpdate?.Invoke(status, message);
    }

    protected async Task<string> AnalyzeComments(string serviceType, string comments)
    {
        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing gathered comments...");

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
        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return await analyzeResponse.Content.ReadAsStringAsync();
    }

    public abstract Task<(string markdownResult, object? additionalData)> GetFeedback();
}