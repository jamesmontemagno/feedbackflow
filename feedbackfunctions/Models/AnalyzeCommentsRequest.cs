using System.Text.Json.Serialization;

namespace FeedbackFunctions;

/// <summary>
/// Request model for analyzing comments
/// </summary>
public class AnalyzeCommentsRequest
{
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;
    
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
