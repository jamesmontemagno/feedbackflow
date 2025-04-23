using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services;

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
    private readonly UserSettingsService _userSettings;
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;
    protected readonly FeedbackStatusUpdate? OnStatusUpdate;

    protected FeedbackService(
        HttpClient http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
    {
        Http = http;
        Configuration = configuration;
        _userSettings = userSettings;
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
        var maxComments = await GetMaxCommentsToAnalyze();

        // Count the number of comments by counting newlines since each comment is on a new line
        var commentCount = comments.Count(c => c == '\n') + 1;
        
        // Calculate estimated analysis time (20 seconds per 100 comments)
        var estimatedSeconds = (int)Math.Ceiling(commentCount * 20.0 / 100);
        
        if (commentCount > maxComments)
        {
            UpdateStatus(
                FeedbackProcessStatus.AnalyzingComments, 
                $"Analyzing {maxComments} comments out of {commentCount} total (analysis limited for performance). " +
                $"Estimated time: {estimatedSeconds} seconds..."
            );
        }
        else
        {
            UpdateStatus(
                FeedbackProcessStatus.AnalyzingComments, 
                $"Analyzing {commentCount} comments. Estimated time: {estimatedSeconds} seconds..."
            );
        }

        var analyzeCode = Configuration["FeedbackApi:AnalyzeCommentsCode"]
            ?? throw new InvalidOperationException("Analyze API code not configured");

        var settings = await _userSettings.GetSettingsAsync();
        var customPrompt = settings.UseCustomPrompts 
            ? settings.ServicePrompts.GetValueOrDefault(serviceType.ToLower())
            : null;

        if (customPrompt != null)
        {
            customPrompt = customPrompt.TrimEnd() + " Format your response in markdown.";
        }

        var analyzeRequestBody = JsonSerializer.Serialize(new
        {
            serviceType,
            comments,
            customPrompt
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

    protected async Task<int> GetMaxCommentsToAnalyze()
    {
        var settings = await _userSettings.GetSettingsAsync();
        return settings.MaxCommentsToAnalyze;
    }
}