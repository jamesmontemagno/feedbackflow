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
    
    /// <summary>
    /// Updates a specific history item
    /// </summary>
    /// <param name="item">The history item to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateHistoryItemAsync(AnalysisHistoryItem item);
    
    /// <summary>
    /// Retrieves a page of history items with optional search filtering
    /// </summary>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to take</param>
    /// <param name="searchTerm">Optional search term to filter results</param>
    /// <returns>A list of analysis history items for the requested page</returns>
    Task<List<AnalysisHistoryItem>> GetHistoryPagedAsync(int skip, int take, string? searchTerm = null);
    
    /// <summary>
    /// Gets the total count of history items with optional search filtering
    /// </summary>
    /// <param name="searchTerm">Optional search term to filter count</param>
    /// <returns>The total number of history items</returns>
    Task<int> GetHistoryCountAsync(string? searchTerm = null);
}
