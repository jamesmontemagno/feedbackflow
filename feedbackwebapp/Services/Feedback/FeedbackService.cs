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

public abstract class FeedbackService : IFeedbackService, IDisposable
{
    private readonly UserSettingsService _userSettings;
    protected readonly HttpClient Http;
    protected readonly IConfiguration Configuration;
    protected readonly string BaseUrl;
    protected readonly FeedbackStatusUpdate? OnStatusUpdate;
    private bool _disposed;

    protected FeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
    {
        Http = http.CreateClient();
        Configuration = configuration;
        _userSettings = userSettings;
        OnStatusUpdate = onStatusUpdate;

        BaseUrl = Configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("Base URL not configured");
    }

    public abstract Task<(string rawComments, int commentCount, object? additionalData)> GetComments(CancellationToken cancellationToken = default);

    public virtual async Task<(string markdownResult, object? additionalData)> AnalyzeComments(
        string comments, int? commentCount = null, object? additionalData = null, CancellationToken cancellationToken = default)
    {
        try 
        {
            return (await AnalyzeCommentsInternal("manual", comments, commentCount ?? 1, cancellationToken: cancellationToken), additionalData);
        }
        catch (Exception)
        {
            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Error analyzing comments");
            throw;
        }
    }

    public virtual async Task<(string markdownResult, object? additionalData)> GetFeedback(CancellationToken cancellationToken = default)
    {
        var (comments, commentCount, additionalData) = await GetComments(cancellationToken);
        return await AnalyzeComments(comments, commentCount, additionalData, cancellationToken);
    }

    protected void UpdateStatus(FeedbackProcessStatus status, string message)
    {
        OnStatusUpdate?.Invoke(status, message);
    }

    protected async Task<string> AnalyzeCommentsInternal(string serviceType, string comments, int commentCount, string? explicitCustomPrompt = null, CancellationToken cancellationToken = default)
    {
        var maxComments = await GetMaxCommentsToAnalyze(cancellationToken);

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

        var settings = await _userSettings.GetSettingsAsync(cancellationToken);
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
        var analyzeResponse = await Http.PostAsync(getAnalysisUrl, analyzeContent, cancellationToken);
        analyzeResponse.EnsureSuccessStatusCode();

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return await analyzeResponse.Content.ReadAsStringAsync(cancellationToken);
    }

    protected async Task<int> GetMaxCommentsToAnalyze(CancellationToken cancellationToken = default)
    {
        var settings = await _userSettings.GetSettingsAsync(cancellationToken);
        return settings.MaxCommentsToAnalyze;
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Http.Dispose();
            // Unsubscribe from any events if needed
            _disposed = true;
        }
    }
}