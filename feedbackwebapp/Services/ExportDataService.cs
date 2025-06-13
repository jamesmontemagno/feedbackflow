using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services;

/// <summary>
/// Service for preparing export data by combining history items with their comments
/// </summary>
public interface IExportDataService
{
    /// <summary>
    /// Prepares history items with their comments for export
    /// </summary>
    /// <param name="historyItems">The history items to export</param>
    /// <returns>History items enriched with comment data</returns>
    Task<IEnumerable<AnalysisHistoryItemWithComments>> PrepareItemsForExportAsync(IEnumerable<AnalysisHistoryItem> historyItems);
}

/// <summary>
/// Implementation of export data service
/// </summary>
public class ExportDataService : IExportDataService
{
    private readonly ICommentsService _commentsService;

    public ExportDataService(ICommentsService commentsService)
    {
        _commentsService = commentsService;
    }

    public async Task<IEnumerable<AnalysisHistoryItemWithComments>> PrepareItemsForExportAsync(IEnumerable<AnalysisHistoryItem> historyItems)
    {
        var historyItemsList = historyItems.ToList();
        if (!historyItemsList.Any())
        {
            return Enumerable.Empty<AnalysisHistoryItemWithComments>();
        }

        // Get feedback IDs
        var feedbackIds = historyItemsList.Select(h => h.Id).ToList();
        
        // Load comments for all feedback items
        var commentsMap = await _commentsService.GetCommentsByFeedbackIdsAsync(feedbackIds);
        
        // Combine history items with their comments
        var result = new List<AnalysisHistoryItemWithComments>();
        
        foreach (var historyItem in historyItemsList)
        {
            // Get comments for this history item
            var comments = commentsMap.TryGetValue(historyItem.Id, out var itemComments) 
                ? itemComments 
                : new List<CommentData>();

            // Create comment threads from the comments
            var commentThreads = new List<CommentThread>();
            
            if (comments.Any())
            {
                // Group comments by thread (for now, create a single thread per history item)
                // In a more complex scenario, we might need to reconstruct the original thread structure
                var thread = new CommentThread
                {
                    Id = historyItem.Id,
                    Title = $"{historyItem.SourceType} Analysis",
                    Description = historyItem.Summary,
                    Author = "System",
                    CreatedAt = historyItem.Timestamp,
                    SourceType = historyItem.SourceType,
                    Comments = comments
                };
                
                commentThreads.Add(thread);
            }

            // Create the extended history item
            var itemWithComments = AnalysisHistoryItemWithComments.CreateFromHistoryItem(historyItem, commentThreads);
            result.Add(itemWithComments);
        }

        return result;
    }
}