using Microsoft.Extensions.AI;

namespace SharedDump.AI;

/// <summary>
/// Service for analyzing feedback comments from various sources
/// </summary>
/// <remarks>
/// This service provides AI-powered analysis of comments and feedback from various platforms.
/// It supports both synchronous and streaming analysis with customizable prompts.
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// // Create the analyzer service
/// var analyzerService = new FeedbackAnalyzerService(configuration);
/// 
/// // Analyze comments from a specific service type
/// string analysis = await analyzerService.AnalyzeCommentsAsync("github", comments);
/// 
/// // Use streaming for real-time updates
/// await foreach (var chunk in analyzerService.GetStreamingAnalysisAsync("youtube", comments))
/// {
///     // Process each chunk of the analysis as it arrives
///     await UpdateUIWithChunk(chunk);
/// }
/// </code>
/// </example>
public interface IFeedbackAnalyzerService
{
    /// <summary>
    /// Creates an AI chat client for analyzing feedback
    /// </summary>
    /// <param name="endpoint">The API endpoint URL</param>
    /// <param name="apiKey">API key for authentication</param>
    /// <param name="deploymentModel">The AI model deployment name</param>
    /// <returns>A configured chat client</returns>
    /// <remarks>
    /// This method creates a properly configured chat client with the specified API parameters.
    /// The client is used internally for communicating with AI services.
    /// </remarks>
    IChatClient CreateClient(string endpoint, string apiKey, string deploymentModel);
    
    /// <summary>
    /// Analyzes comments from a specific service type
    /// </summary>
    /// <param name="serviceType">The service type (e.g., "github", "youtube", "reddit")</param>
    /// <param name="comments">The raw comments to analyze</param>
    /// <returns>Analysis results formatted in markdown</returns>
    /// <remarks>
    /// This method uses predefined prompts based on the service type to analyze the comments.
    /// </remarks>
    Task<string> AnalyzeCommentsAsync(string serviceType, string comments);
    
    /// <summary>
    /// Analyzes comments with a custom system prompt
    /// </summary>
    /// <param name="serviceType">The service type (e.g., "github", "youtube", "reddit")</param>
    /// <param name="comments">The raw comments to analyze</param>
    /// <param name="customSystemPrompt">Custom prompt to override the default</param>
    /// <returns>Analysis results formatted in markdown</returns>
    /// <remarks>
    /// This method allows for overriding the default prompt with a custom one,
    /// which is useful for specialized analysis needs.
    /// </remarks>
    Task<string> AnalyzeCommentsAsync(string serviceType, string comments, string? customSystemPrompt);
    
    /// <summary>
    /// Gets streaming analysis of comments for real-time updates
    /// </summary>
    /// <param name="serviceType">The service type (e.g., "github", "youtube", "reddit")</param>
    /// <param name="comments">The raw comments to analyze</param>
    /// <returns>An async enumerable of analysis chunks</returns>
    /// <remarks>
    /// This method provides a streaming interface for receiving analysis results in real-time.
    /// It's particularly useful for long analyses where showing incremental results improves user experience.
    /// </remarks>
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments);
    
    /// <summary>
    /// Gets streaming analysis of comments using a custom system prompt
    /// </summary>
    /// <param name="serviceType">The service type (e.g., "github", "youtube", "reddit")</param>
    /// <param name="comments">The raw comments to analyze</param>
    /// <param name="customSystemPrompt">Custom prompt to override the default</param>
    /// <returns>An async enumerable of analysis chunks</returns>
    /// <remarks>
    /// This method combines streaming capabilities with custom prompt functionality,
    /// allowing for specialized real-time analysis.
    /// </remarks>
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments, string? customSystemPrompt);
}