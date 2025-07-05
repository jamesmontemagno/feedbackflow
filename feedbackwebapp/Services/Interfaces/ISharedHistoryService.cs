namespace FeedbackWebApp.Services.Interfaces;

using SharedDump.Models;

/// <summary>
/// Service for managing user's saved shared analysis history and sharing functionality
/// </summary>
/// <remarks>
/// This service provides functionality to retrieve, save, delete, and share user's analyses
/// in the backend storage. It manages both the user's personal collection of saved shared analyses
/// and the ability to create new shared analyses.
/// </remarks>
/// <example>
/// <code>
/// // Example usage of ISharedHistoryService
/// public class SharedHistoryComponent
/// {
///     private readonly ISharedHistoryService _sharedHistoryService;
///     
///     public SharedHistoryComponent(ISharedHistoryService sharedHistoryService)
///     {
///         _sharedHistoryService = sharedHistoryService;
///     }
///     
///     public async Task ShowUsersSavedAnalysesAsync()
///     {
///         var savedAnalyses = await _sharedHistoryService.GetUsersSavedAnalysesAsync();
///         // Process saved analyses
///     }
///     
///     public async Task ShareNewAnalysisAsync(AnalysisData analysis)
///     {
///         var sharedId = await _sharedHistoryService.ShareAnalysisAsync(analysis);
///         // Use the shared ID
///     }
/// }
/// </code>
/// </example>
public interface ISharedHistoryService
{
    /// <summary>
    /// Retrieves all saved shared analyses for the authenticated user
    /// </summary>
    /// <returns>A list of shared analysis entities owned by the user</returns>
    Task<List<SharedAnalysisEntity>> GetUsersSavedAnalysesAsync();
    
    /// <summary>
    /// Deletes a specific shared analysis if the user owns it
    /// </summary>
    /// <param name="id">The ID of the shared analysis to delete</param>
    /// <returns>True if deleted successfully, false if not found or not authorized</returns>
    Task<bool> DeleteSharedAnalysisAsync(string id);
    
    /// <summary>
    /// Gets the full analysis data for a shared analysis by ID
    /// </summary>
    /// <param name="id">The unique identifier of the shared analysis</param>
    /// <returns>The complete analysis data, or null if not found</returns>
    Task<AnalysisData?> GetSharedAnalysisDataAsync(string id);
    
    /// <summary>
    /// Gets the count of saved analyses for the authenticated user
    /// </summary>
    /// <returns>The number of saved analyses</returns>
    Task<int> GetSavedAnalysesCountAsync();
    
    /// <summary>
    /// Searches user's saved analyses by title or summary
    /// </summary>
    /// <param name="searchTerm">The search term to filter by</param>
    /// <returns>A list of matching shared analysis entities</returns>
    Task<List<SharedAnalysisEntity>> SearchUsersSavedAnalysesAsync(string searchTerm);
    
    // Sharing functionality (merged from IAnalysisSharingService)
    
    /// <summary>
    /// Shares an analysis result and returns a unique identifier for the shared analysis
    /// </summary>
    /// <param name="analysis">The analysis data to share</param>
    /// <param name="isPublic">Whether the analysis should be publicly accessible</param>
    /// <returns>A unique identifier for the shared analysis</returns>
    Task<string> ShareAnalysisAsync(AnalysisData analysis, bool isPublic = false);
    
    /// <summary>
    /// Updates the visibility (public/private) of a shared analysis
    /// </summary>
    /// <param name="analysisId">The ID of the analysis to update</param>
    /// <param name="isPublic">Whether the analysis should be publicly accessible</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateAnalysisVisibilityAsync(string analysisId, bool isPublic);
    
    /// <summary>
    /// Gets the public share link for an analysis (only if it's public)
    /// </summary>
    /// <param name="analysisId">The ID of the analysis</param>
    /// <returns>The public share URL, or null if analysis is not public</returns>
    Task<string?> GetPublicShareLinkAsync(string analysisId);
    
    /// <summary>
    /// Retrieves a shared analysis by its identifier (alias for GetSharedAnalysisDataAsync)
    /// </summary>
    /// <param name="id">The unique identifier of the shared analysis</param>
    /// <returns>The shared analysis data, or null if not found</returns>
    Task<AnalysisData?> GetSharedAnalysisAsync(string id);
    
    /// <summary>
    /// Retrieves the history of shared analyses
    /// </summary>
    /// <returns>A list of analysis history items that have been shared</returns>
    Task<List<AnalysisHistoryItem>> GetSharedAnalysisHistoryAsync();
}
