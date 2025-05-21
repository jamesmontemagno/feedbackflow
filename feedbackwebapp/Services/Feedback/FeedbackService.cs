using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Feedback;

public enum FeedbackProcessStatus
{
    GatheringComments,
    AnalyzingComments,
    Completed
}

public delegate void FeedbackStatusUpdate(FeedbackProcessStatus status, string message);

public abstract class FeedbackService : IFeedbackService
{
    private readonly UserSettingsService _userSettings;
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;
    protected readonly FeedbackStatusUpdate? OnStatusUpdate;

    protected FeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
    {
        Http = http.CreateClient("DefaultClient");
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

    protected async Task<string> AnalyzeCommentsInternal(string serviceType, string comments, int commentCount, string? explicitCustomPrompt = null)
    {
        var maxComments = await GetMaxCommentsToAnalyze();

        // Calculate estimated analysis time (20 seconds per 100 comments)
        var estimatedSeconds = Math.Max(10, (int)Math.Ceiling(commentCount * 20.0 / 100));

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
        string? customPrompt = null;

        // If an explicit custom prompt is provided, use it (for manual mode)
        if (!string.IsNullOrEmpty(explicitCustomPrompt))
        {
            customPrompt = explicitCustomPrompt;
        }
        // Otherwise, check user settings for custom prompts (for other service types)
        else if (settings.UseCustomPrompts)
        {
            customPrompt = settings.ServicePrompts.GetValueOrDefault(serviceType.ToLower());
        }

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

    /// <summary>
    /// Gets raw comments directly from the source
    /// </summary>
    public abstract Task<(string rawComments, object? additionalData)> GetComments();

    /// <summary>
    /// Analyzes comments to produce insights
    /// </summary>
    public abstract Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, object? additionalData = null);
    
    /// <summary>
    /// Gets and analyzes feedback in a single operation
    /// </summary>
    public abstract Task<(string markdownResult, object? additionalData)> GetFeedback();

    protected async Task<int> GetMaxCommentsToAnalyze()
    {
        var settings = await _userSettings.GetSettingsAsync();
        return settings.MaxCommentsToAnalyze;
    }
}