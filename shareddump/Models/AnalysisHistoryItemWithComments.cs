namespace SharedDump.Models;

/// <summary>
/// Extended analysis history item that includes comments for export operations
/// </summary>
public record AnalysisHistoryItemWithComments : AnalysisHistoryItem
{
    /// <summary>
    /// Create an extended item from a regular history item and comment data
    /// </summary>
    /// <param name="historyItem">The base history item</param>
    /// <param name="commentThreads">The comment threads to include</param>
    /// <returns>Extended history item with comments</returns>
    public static AnalysisHistoryItemWithComments CreateFromHistoryItem(
        AnalysisHistoryItem historyItem, 
        List<CommentThread> commentThreads)
    {
        return new AnalysisHistoryItemWithComments
        {
            Id = historyItem.Id,
            Timestamp = historyItem.Timestamp,
            Summary = historyItem.Summary,
            FullAnalysis = historyItem.FullAnalysis,
            SourceType = historyItem.SourceType,
            UserInput = historyItem.UserInput,
            IsShared = historyItem.IsShared,
            SharedId = historyItem.SharedId,
            SharedDate = historyItem.SharedDate,
            CommentThreads = commentThreads
        };
    }
}