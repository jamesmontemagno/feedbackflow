using System.Text.Json.Serialization;
using SharedDump.Models;

namespace FeedbackFunctions;

/// <summary>
/// Request model for analyzing comments
/// </summary>
public class AnalyzeCommentsRequest
{
    /// <summary>
    /// Legacy: Full comment data as a string (for backward compatibility)
    /// </summary>
    [JsonPropertyName("comments")]
    public string? Comments { get; set; }
    
    /// <summary>
    /// New: Minified comment threads for efficient data transfer
    /// When provided, this takes precedence over the Comments string
    /// </summary>
    [JsonPropertyName("minifiedThreads")]
    public List<MinifiedCommentThread>? MinifiedThreads { get; set; }
    
    [JsonPropertyName("serviceType")]
    public string ServiceType { get; set; } = string.Empty;

    [JsonPropertyName("customPrompt")]
    public string? CustomPrompt { get; set; }
    
    /// <summary>
    /// Optional prompt type name (e.g., "ProductFeedback", "CompetitorAnalysis", "GeneralAnalysis")
    /// When specified, the backend will look up the standard prompt for this type.
    /// If CustomPrompt is also provided, CustomPrompt takes precedence.
    /// </summary>
    [JsonPropertyName("promptType")]
    public string? PromptType { get; set; }
}
