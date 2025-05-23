namespace FeedbackWebApp.Services.Interfaces;

using SharedDump.Models;

/// <summary>
/// Service for sharing analysis results and retrieving shared analyses
/// </summary>
/// <remarks>
/// This service enables users to share analysis results with others and retrieve
/// previously shared analyses. It also tracks sharing history.
/// </remarks>
/// <example>
/// <code>
/// // Example of sharing an analysis
/// public class SharingComponent
/// {
///     private readonly IAnalysisSharingService _sharingService;
///     
///     public SharingComponent(IAnalysisSharingService sharingService)
///     {
///         _sharingService = sharingService;
///     }
///     
///     public async Task ShareMyAnalysisAsync(AnalysisData analysis, string historyItemId)
///     {
///         var sharedId = await _sharingService.ShareAnalysisAsync(analysis);
///         await _sharingService.UpdateHistoryItemWithShareInfoAsync(historyItemId, sharedId);
///         return sharedId;
///     }
///     
///     public async Task<AnalysisData?> GetSharedAnalysisAsync(string id)
///     {
///         return await _sharingService.GetSharedAnalysisAsync(id);
///     }
/// }
/// </code>
/// </example>
public interface IAnalysisSharingService
{
    /// <summary>
    /// Shares an analysis result and returns a unique identifier for the shared analysis
    /// </summary>
    /// <param name="analysis">The analysis data to share</param>
    /// <returns>A unique identifier for the shared analysis</returns>
    Task<string> ShareAnalysisAsync(AnalysisData analysis);
    
    /// <summary>
    /// Retrieves a shared analysis by its identifier
    /// </summary>
    /// <param name="id">The unique identifier of the shared analysis</param>
    /// <returns>The shared analysis data, or null if not found</returns>
    Task<AnalysisData?> GetSharedAnalysisAsync(string id);
    
    /// <summary>
    /// Retrieves the history of shared analyses
    /// </summary>
    /// <returns>A list of analysis history items that have been shared</returns>
    Task<List<AnalysisHistoryItem>> GetSharedAnalysisHistoryAsync();
    
    /// <summary>
    /// Updates a history item with information about it being shared
    /// </summary>
    /// <param name="historyItemId">The ID of the history item</param>
    /// <param name="sharedId">The shared analysis ID</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateHistoryItemWithShareInfoAsync(string historyItemId, string sharedId);
}