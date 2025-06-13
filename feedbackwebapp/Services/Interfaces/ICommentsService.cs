using SharedDump.Models;

namespace FeedbackWebApp.Services.Interfaces;

/// <summary>
/// Service interface for managing comments stored in a separate IndexedDB database
/// </summary>
public interface ICommentsService : IAsyncDisposable
{
    /// <summary>
    /// Add a new comment for a feedback item
    /// </summary>
    /// <param name="feedbackId">The ID of the feedback item</param>
    /// <param name="comment">The comment data to add</param>
    /// <returns>The added comment with generated ID</returns>
    Task<CommentData> AddCommentAsync(string feedbackId, CommentData comment);

    /// <summary>
    /// Edit an existing comment
    /// </summary>
    /// <param name="commentId">The ID of the comment to edit</param>
    /// <param name="updatedComment">The updated comment data</param>
    /// <returns>The updated comment</returns>
    Task<CommentData> EditCommentAsync(string commentId, CommentData updatedComment);

    /// <summary>
    /// Delete a specific comment
    /// </summary>
    /// <param name="commentId">The ID of the comment to delete</param>
    Task DeleteCommentAsync(string commentId);

    /// <summary>
    /// Get all comments for a specific feedback item
    /// </summary>
    /// <param name="feedbackId">The ID of the feedback item</param>
    /// <returns>List of comments for the feedback item</returns>
    Task<List<CommentData>> GetCommentsByFeedbackIdAsync(string feedbackId);

    /// <summary>
    /// Get comments for multiple feedback items (bulk operation for export)
    /// </summary>
    /// <param name="feedbackIds">List of feedback item IDs</param>
    /// <returns>Dictionary mapping feedback IDs to their comments</returns>
    Task<Dictionary<string, List<CommentData>>> GetCommentsByFeedbackIdsAsync(IEnumerable<string> feedbackIds);

    /// <summary>
    /// Delete all comments for a specific feedback item (for data consistency)
    /// </summary>
    /// <param name="feedbackId">The ID of the feedback item</param>
    /// <returns>Number of comments deleted</returns>
    Task<int> DeleteCommentsByFeedbackIdAsync(string feedbackId);

    /// <summary>
    /// Clear all comments (for testing and maintenance)
    /// </summary>
    Task ClearAllCommentsAsync();

    /// <summary>
    /// Migrate existing comments from history items to the comments database
    /// </summary>
    /// <param name="historyItems">History items containing comments to migrate</param>
    /// <returns>Migration results with count and any errors</returns>
    Task<(int migrated, List<string> errors)> MigrateCommentsFromHistoryAsync(IEnumerable<AnalysisHistoryItem> historyItems);
}