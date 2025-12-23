using System.Text;
using System.Text.Json;
using FeedbackWebApp.Services.Interfaces;
using FeedbackWebApp.Services.Authentication;
using SharedDump.Utils;
using SharedDump.AI;
using SharedDump.Services;

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
    protected readonly IHttpClientFactory HttpFactory;
    protected readonly IConfiguration Configuration;
    protected readonly IAuthenticationHeaderService AuthHeaderService;
    protected readonly string BaseUrl;
    protected readonly FeedbackStatusUpdate? OnStatusUpdate;
    /// <summary>
    /// A temporary, per-request prompt override supplied from the UI that is NOT persisted in user settings.
    /// </summary>
    internal string? TemporaryPrompt { get; set; }

    protected FeedbackService(
        IHttpClientFactory http, 
        IConfiguration configuration, 
        UserSettingsService userSettings,
        IAuthenticationHeaderService authHeaderService,
        FeedbackStatusUpdate? onStatusUpdate = null)
    {
        Http = http.CreateClient();
        HttpFactory = http;
        Configuration = configuration;
        AuthHeaderService = authHeaderService;
        _userSettings = userSettings;
        OnStatusUpdate = onStatusUpdate;

        BaseUrl = Configuration["FeedbackApi:BaseUrl"]
            ?? throw new InvalidOperationException("Base URL not configured");
    }

    public abstract Task<(string rawComments, int commentCount, object? additionalData)> GetComments(int? maxCommentsOverride = null);

    public virtual async Task<(string markdownResult, object? additionalData)> AnalyzeComments(
        string comments, int? commentCount = null, object? additionalData = null)
    {
        try 
        {
            return (await AnalyzeCommentsInternal("manual", comments, commentCount ?? 1), additionalData);
        }
        catch (Exception)
        {
            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Error analyzing comments");
            throw;
        }
    }

    public virtual async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        var (comments, commentCount, additionalData) = await GetComments();
        return await AnalyzeComments(comments, commentCount, additionalData);
    }

    public void SetTemporaryPrompt(string? prompt)
    {
        TemporaryPrompt = string.IsNullOrWhiteSpace(prompt) ? null : prompt.Trim();
    }

    public void ClearTemporaryPrompt() => TemporaryPrompt = null;

    public string? GetTemporaryPrompt() => TemporaryPrompt;

    protected void UpdateStatus(FeedbackProcessStatus status, string message)
    {
        OnStatusUpdate?.Invoke(status, message);
    }

    /// <summary>
    /// Maximum character length for comments sent to analysis.
    /// This prevents timeouts and rate limiting from the AI service.
    /// ~300K characters ≈ ~75K tokens for most content.
    /// </summary>
    private const int MaxCommentsCharacterLength = 300_000;

    /// <summary>
    /// Prepares comments for analysis by converting platform data to optimized text format.
    /// This reduces payload size and token usage significantly.
    /// </summary>
    /// <param name="serviceType">Platform type (github, reddit, hackernews, etc.)</param>
    /// <param name="rawComments">Raw JSON comments from the backend</param>
    /// <param name="additionalData">Parsed platform-specific data object</param>
    /// <param name="originalCommentCount">Original number of comments before limiting</param>
    /// <returns>Tuple of (prepared text, actual comment count included)</returns>
    protected async Task<(string preparedText, int actualCommentCount)> PrepareCommentsForAnalysis(
        string serviceType,
        string rawComments,
        object? additionalData,
        int originalCommentCount)
    {
        var settings = await _userSettings.GetSettingsAsync();
        var maxComments = settings.MaxCommentsToAnalyze;
        var useSlimmed = settings.UseSlimmedComments;

        // Debug logging
        var rawLength = rawComments?.Length ?? 0;
        var additionalDataType = additionalData?.GetType().FullName ?? "null";
        Console.WriteLine($"[PrepareCommentsForAnalysis] START: serviceType={serviceType}, rawCommentsLength={rawLength}, additionalDataType={additionalDataType}, originalCommentCount={originalCommentCount}, maxComments={maxComments}, useSlimmed={useSlimmed}");

        string preparedText = rawComments ?? string.Empty;
        int actualCount = originalCommentCount;
        string conversionSource = "raw (no conversion)";

        // If we have structured data, use CommentPreparer for optimal conversion
        if (additionalData is not null)
        {
            Console.WriteLine($"[PrepareCommentsForAnalysis] Trying CommentPreparer.PrepareForAnalysis with additionalData type: {additionalDataType}");
            
            var (text, count) = CommentPreparer.PrepareForAnalysis(
                additionalData, 
                maxComments, 
                useSlimmed);

            Console.WriteLine($"[PrepareCommentsForAnalysis] CommentPreparer result: textLength={text?.Length ?? 0}, count={count}");

            if (!string.IsNullOrEmpty(text))
            {
                preparedText = text;
                actualCount = count > 0 ? count : originalCommentCount;
                conversionSource = $"CommentPreparer.PrepareForAnalysis ({additionalDataType})";
                Console.WriteLine($"[PrepareCommentsForAnalysis] Using CommentPreparer result");
            }
            else
            {
                Console.WriteLine($"[PrepareCommentsForAnalysis] CommentPreparer returned empty, will try JSON fallback");
            }
        }
        
        // Fallback: try to parse the raw JSON and convert it (only if we haven't already converted)
        if (conversionSource.StartsWith("raw") && !string.IsNullOrWhiteSpace(rawComments) && 
            (rawComments.TrimStart().StartsWith('[') || rawComments.TrimStart().StartsWith('{')))
        {
            Console.WriteLine($"[PrepareCommentsForAnalysis] Trying JSON fallback with PrepareJsonForAnalysis");
            
            var (text, count) = CommentPreparer.PrepareJsonForAnalysis(
                rawComments,
                serviceType,
                maxComments,
                useSlimmed);

            Console.WriteLine($"[PrepareCommentsForAnalysis] JSON fallback result: textLength={text?.Length ?? 0}, count={count}");

            if (!string.IsNullOrEmpty(text) && count > 0)
            {
                preparedText = text;
                actualCount = count;
                conversionSource = "CommentPreparer.PrepareJsonForAnalysis";
                Console.WriteLine($"[PrepareCommentsForAnalysis] Using JSON fallback result");
            }
            else
            {
                Console.WriteLine($"[PrepareCommentsForAnalysis] JSON fallback failed or returned no comments");
            }
        }

        Console.WriteLine($"[PrepareCommentsForAnalysis] After conversion: textLength={preparedText.Length}, actualCount={actualCount}, source={conversionSource}");

        // Apply hard character limit to prevent AI rate limiting/timeouts
        if (preparedText.Length > MaxCommentsCharacterLength)
        {
            var originalLength = preparedText.Length;
            preparedText = TruncateToCharacterLimit(preparedText, MaxCommentsCharacterLength);
            
            // Estimate reduced comment count based on truncation ratio
            var ratio = (double)preparedText.Length / originalLength;
            actualCount = Math.Max(1, (int)(actualCount * ratio));
            
            Console.WriteLine($"[PrepareCommentsForAnalysis] TRUNCATED: {originalLength:N0} -> {preparedText.Length:N0} chars, actualCount adjusted to {actualCount}");
            
            UpdateStatus(FeedbackProcessStatus.AnalyzingComments, 
                $"Content truncated from {originalLength:N0} to {preparedText.Length:N0} characters for optimal analysis...");
        }

        Console.WriteLine($"[PrepareCommentsForAnalysis] END: finalTextLength={preparedText.Length}, finalCount={actualCount}, conversionSource={conversionSource}");

        return (preparedText, actualCount);
    }

    /// <summary>
    /// Truncates text to a maximum character limit, ensuring we don't cut in the middle of a comment.
    /// </summary>
    private static string TruncateToCharacterLimit(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        // Find a good break point (end of a comment/paragraph)
        var truncated = text[..maxLength];
        
        // Try to find the last complete comment (look for double newline or markdown separator)
        var lastBreak = truncated.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (lastBreak > maxLength * 0.7) // Only use if we keep at least 70% of content
        {
            truncated = truncated[..lastBreak];
        }
        else
        {
            // Fall back to last newline
            lastBreak = truncated.LastIndexOf('\n');
            if (lastBreak > maxLength * 0.9)
            {
                truncated = truncated[..lastBreak];
            }
        }

        return truncated + "\n\n---\n_[Content truncated for analysis - showing first portion of comments]_";
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

        var analyzeCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("Analyze API code not configured");

        var settings = await _userSettings.GetSettingsAsync();
        
        // Determine what to send to the backend:
        // - If temporary prompt exists (user customized in UI), send as customPrompt
        // - If explicit custom prompt provided, send as customPrompt
        // - If user's default is Custom prompt, send their custom prompt
        // - Otherwise, send promptType name to let backend look up the standard prompt
        string? customPrompt = null;
        string? promptType = null;

        if (!string.IsNullOrWhiteSpace(TemporaryPrompt))
        {
            // User customized prompt in UI - send full prompt
            customPrompt = TemporaryPrompt.TrimEnd() + " Format your response in markdown.";
        }
        else if (!string.IsNullOrEmpty(explicitCustomPrompt))
        {
            // Explicit custom prompt (e.g., from manual input) - send full prompt
            customPrompt = explicitCustomPrompt.TrimEnd() + " Format your response in markdown.";
        }
        else if (settings.SelectedPromptType == PromptType.Custom)
        {
            // User's default is custom prompt - send their custom prompt
            customPrompt = (settings.UniversalPrompt ?? FeedbackAnalyzerService.GetUniversalPrompt()).TrimEnd() + " Format your response in markdown.";
        }
        else
        {
            // Use standard prompt type - send just the type name to minimize payload
            promptType = settings.SelectedPromptType.ToString();
        }

        var analyzeRequestBody = JsonSerializer.Serialize(new
        {
            serviceType,
            comments,
            customPrompt,
            promptType,
            useSlimmedComments = settings.UseSlimmedComments
        });

        var analyzeContent = new StringContent(
            analyzeRequestBody,
            Encoding.UTF8,
            "application/json");

        var getAnalysisUrl = $"{BaseUrl}/api/AnalyzeComments?code={Uri.EscapeDataString(analyzeCode)}";
        var analyzeResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Post, getAnalysisUrl, analyzeContent);

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return await analyzeResponse.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Analyzes comments using optimized text conversion for reduced payload size.
    /// This method should be preferred when structured platform data is available.
    /// </summary>
    /// <param name="serviceType">Platform type (github, reddit, hackernews, etc.)</param>
    /// <param name="rawComments">Raw JSON comments from the backend</param>
    /// <param name="originalCommentCount">Original number of comments before limiting</param>
    /// <param name="additionalData">Parsed platform-specific data object</param>
    /// <param name="explicitCustomPrompt">Optional custom prompt override</param>
    /// <returns>Markdown analysis result</returns>
    protected async Task<string> AnalyzeCommentsWithOptimization(
        string serviceType, 
        string rawComments, 
        int originalCommentCount,
        object? additionalData,
        string? explicitCustomPrompt = null)
    {
        // Prepare comments using the optimized converter
        var (preparedComments, actualCommentCount) = await PrepareCommentsForAnalysis(
            serviceType, 
            rawComments, 
            additionalData, 
            originalCommentCount);

        // Use the prepared comments for analysis
        return await AnalyzeCommentsInternal(serviceType, preparedComments, actualCommentCount, explicitCustomPrompt);
    }

    protected async Task<string> AnalyzeCommentsInternalWithoutStatusUpdate(string serviceType, string comments, int commentCount, string? explicitCustomPrompt = null)
    {
        var maxComments = await GetMaxCommentsToAnalyze();

        var analyzeCode = Configuration["FeedbackApi:FunctionsKey"]
            ?? throw new InvalidOperationException("Analyze API code not configured");

        var settings = await _userSettings.GetSettingsAsync();
        
        // Determine what to send to the backend:
        // - If temporary prompt exists (user customized in UI), send as customPrompt
        // - If explicit custom prompt provided, send as customPrompt
        // - If user's default is Custom prompt, send their custom prompt
        // - Otherwise, send promptType name to let backend look up the standard prompt
        string? customPrompt = null;
        string? promptType = null;

        if (!string.IsNullOrWhiteSpace(TemporaryPrompt))
        {
            // User customized prompt in UI - send full prompt
            customPrompt = TemporaryPrompt.TrimEnd() + " Format your response in markdown.";
        }
        else if (!string.IsNullOrEmpty(explicitCustomPrompt))
        {
            // Explicit custom prompt (e.g., from manual input) - send full prompt
            customPrompt = explicitCustomPrompt.TrimEnd() + " Format your response in markdown.";
        }
        else if (settings.SelectedPromptType == PromptType.Custom)
        {
            // User's default is custom prompt - send their custom prompt
            customPrompt = (settings.UniversalPrompt ?? FeedbackAnalyzerService.GetUniversalPrompt()).TrimEnd() + " Format your response in markdown.";
        }
        else
        {
            // Use standard prompt type - send just the type name to minimize payload
            promptType = settings.SelectedPromptType.ToString();
        }

        var analyzeRequestBody = JsonSerializer.Serialize(new
        {
            serviceType,
            comments,
            customPrompt,
            promptType,
            useSlimmedComments = settings.UseSlimmedComments
        });

        var analyzeContent = new StringContent(
            analyzeRequestBody,
            Encoding.UTF8,
            "application/json");

        var getAnalysisUrl = $"{BaseUrl}/api/AnalyzeComments?code={Uri.EscapeDataString(analyzeCode)}";
        var analyzeResponse = await SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod.Post, getAnalysisUrl, analyzeContent);

        // Note: This method doesn't call UpdateStatus to avoid multiple completion status updates
        return await analyzeResponse.Content.ReadAsStringAsync();
    }

    protected async Task<int> GetMaxCommentsToAnalyze(int? maxCommentsOverride = null)
    {
        if (maxCommentsOverride.HasValue)
            return maxCommentsOverride.Value;
            
        var settings = await _userSettings.GetSettingsAsync();
        return settings.MaxCommentsToAnalyze;
    }

    /// <summary>
    /// Creates an authenticated HTTP request message with proper headers
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="requestUri">Request URI</param>
    /// <param name="content">Optional content</param>
    /// <returns>Configured HttpRequestMessage</returns>
    protected async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string requestUri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, requestUri);
        if (content != null)
        {
            request.Content = content;
        }
        
        await AuthHeaderService.AddAuthenticationHeadersAsync(request);
        return request;
    }

    /// <summary>
    /// Sends an authenticated HTTP request
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="requestUri">Request URI</param>
    /// <param name="content">Optional content</param>
    /// <returns>HTTP response message</returns>
    protected async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(HttpMethod method, string requestUri, HttpContent? content = null)
    {
        var request = await CreateAuthenticatedRequestAsync(method, requestUri, content);
        return await Http.SendAsync(request);
    }

    /// <summary>
    /// Sends an authenticated HTTP request and ensures success, with special handling for usage limit errors
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="requestUri">Request URI</param>
    /// <param name="content">Optional content</param>
    /// <returns>HTTP response message</returns>
    /// <exception cref="UsageLimitExceededException">Thrown when usage limits are exceeded</exception>
    protected async Task<HttpResponseMessage> SendAuthenticatedRequestWithUsageLimitCheckAsync(HttpMethod method, string requestUri, HttpContent? content = null)
    {
        var response = await SendAuthenticatedRequestAsync(method, requestUri, content);
        
        // Check for usage limit error before calling EnsureSuccessStatusCode
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            if (UsageLimitErrorHelper.TryParseUsageLimitError(errorContent, response.StatusCode, out var limitError) && limitError != null)
            {
                throw new UsageLimitExceededException(limitError);
            }
        }
        
        response.EnsureSuccessStatusCode();
        return response;
    }
}