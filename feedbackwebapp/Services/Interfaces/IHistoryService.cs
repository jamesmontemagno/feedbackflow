
namespace FeedbackWebApp.Services.Interfaces;

using SharedDump.Models;

/// <summary>
/// Service for managing analysis history items
/// </summary>
/// <remarks>
/// This service provides functionality to retrieve, save, and delete history items
/// for feedback analyses performed in the application.
/// </remarks>
/// <example>
/// <code>
/// // Example usage of IHistoryService
/// public class HistoryComponent
/// {
///     private readonly IHistoryService _historyService;
///     
///     public HistoryComponent(IHistoryService historyService)
///     {
///         _historyService = historyService;
///     }
///     
///     public async Task ShowHistoryAsync()
///     {
///         var historyItems = await _historyService.GetHistoryAsync();
///         // Process history items
///     }
///     
///     public async Task SaveAnalysisAsync(AnalysisHistoryItem item)
///     {
///         await _historyService.SaveToHistoryAsync(item);
///     }
/// }
/// </code>
/// </example>
public interface IHistoryService
{
    /// <summary>
    /// Retrieves the history of analyses performed
    /// </summary>
    /// <returns>A list of analysis history items</returns>
    Task<List<AnalysisHistoryItem>> GetHistoryAsync();
    
    /// <summary>
    /// Saves an analysis result to history
    /// </summary>
    /// <param name="item">The analysis history item to save</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SaveToHistoryAsync(AnalysisHistoryItem item);
    
    /// <summary>
    /// Deletes a specific history item by ID
    /// </summary>
    /// <param name="id">The ID of the history item to delete</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task DeleteHistoryItemAsync(string id);
    
    /// <summary>
    /// Clears all history items
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ClearHistoryAsync();
}
