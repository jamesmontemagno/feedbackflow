using Microsoft.Extensions.AI;

namespace SharedDump.AI;

/// <summary>
/// Interface for services that analyze feedback comments from various sources.
/// </summary>
public interface IFeedbackAnalyzerService
{
    /// <summary>
    /// Creates a chat client for AI analysis.
    /// </summary>
    /// <param name="endpoint">The endpoint URL for the AI service.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="deploymentModel">The name of the deployed model to use.</param>
    /// <returns>A chat client for making API requests.</returns>
    IChatClient CreateClient(string endpoint, string apiKey, string deploymentModel);
    
    /// <summary>
    /// Analyzes comments using the default prompt for a specific service type.
    /// </summary>
    /// <param name="serviceType">The type of service (e.g., "youtube", "github").</param>
    /// <param name="comments">The comments to analyze.</param>
    /// <returns>Analysis results as a string.</returns>
    Task<string> AnalyzeCommentsAsync(string serviceType, string comments);
    
    /// <summary>
    /// Analyzes comments using a custom system prompt or the default for a service type.
    /// </summary>
    /// <param name="serviceType">The type of service (e.g., "youtube", "github").</param>
    /// <param name="comments">The comments to analyze.</param>
    /// <param name="customSystemPrompt">Optional custom system prompt to use instead of the default.</param>
    /// <returns>Analysis results as a string.</returns>
    Task<string> AnalyzeCommentsAsync(string serviceType, string comments, string? customSystemPrompt);
    
    /// <summary>
    /// Gets a streaming analysis of comments using the default prompt for a service type.
    /// </summary>
    /// <param name="serviceType">The type of service (e.g., "youtube", "github").</param>
    /// <param name="comments">The comments to analyze.</param>
    /// <returns>A stream of analysis updates.</returns>
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments);
    
    /// <summary>
    /// Gets a streaming analysis of comments using a custom system prompt or the default.
    /// </summary>
    /// <param name="serviceType">The type of service (e.g., "youtube", "github").</param>
    /// <param name="comments">The comments to analyze.</param>
    /// <param name="customSystemPrompt">Optional custom system prompt to use instead of the default.</param>
    /// <returns>A stream of analysis updates.</returns>
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments, string? customSystemPrompt);
}