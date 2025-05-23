using SharedDump.Models;

namespace SharedDump.AI;

/// <summary>
/// Service for sharing analysis results and managing shared analysis history
/// </summary>
/// <remarks>
/// This service provides a centralized way to share analysis results with others,
/// retrieve shared analyses, and track sharing history.
/// </remarks>
/// <example>
/// <code>
/// // Share an analysis
/// var sharingService = new AnalysisSharingService();
/// var analysisData = new AnalysisData
/// {
///     Title = "GitHub Feedback Analysis",
///     Content = "## Analysis Results\n\nKey findings from the feedback...",
///     Source = "GitHub",
///     CreatedAt = DateTimeOffset.Now
/// };
/// 
/// string shareId = await sharingService.ShareAnalysisAsync(analysisData);
/// Console.WriteLine($"Analysis shared with ID: {shareId}");
/// 
/// // Retrieve a shared analysis
/// var sharedAnalysis = await sharingService.GetSharedAnalysisAsync(shareId);
/// </code>
/// </example>
public interface IAnalysisSharingService
{
    /// <summary>
    /// Shares an analysis and returns a unique identifier
    /// </summary>
    /// <param name="analysis">The analysis data to share</param>
    /// <returns>A unique identifier for accessing the shared analysis</returns>
    /// <remarks>
    /// This method stores the analysis in a persistent storage and returns
    /// an ID that can be used to retrieve it later.
    /// </remarks>
    Task<string> ShareAnalysisAsync(AnalysisData analysis);
    
    /// <summary>
    /// Retrieves a shared analysis by its identifier
    /// </summary>
    /// <param name="id">The unique identifier of the shared analysis</param>
    /// <returns>The analysis data if found, otherwise null</returns>
    Task<AnalysisData?> GetSharedAnalysisAsync(string id);
    
    /// <summary>
    /// Retrieves the history of shared analyses
    /// </summary>
    /// <returns>A list of records representing shared analyses</returns>
    /// <remarks>
    /// This method returns all shared analyses in chronological order.
    /// </remarks>
    Task<List<SharedAnalysisRecord>> GetSharedAnalysisHistoryAsync();
    
    /// <summary>
    /// Saves a shared analysis record to history
    /// </summary>
    /// <param name="record">The record of a shared analysis</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SaveSharedAnalysisToHistoryAsync(SharedAnalysisRecord record);
}