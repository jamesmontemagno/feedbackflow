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
}
