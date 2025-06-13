using Microsoft.JSInterop;
using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services;

/// <summary>
/// Service for managing comments stored in a separate IndexedDB database
/// </summary>
public class CommentsService : ICommentsService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private bool _initialized;

    public CommentsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            _initialized = true;
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js");
        }
        catch (InvalidOperationException)
        {
            // We're probably in prerendering
            _initialized = false;
        }
    }

    public async Task<CommentData> AddCommentAsync(string feedbackId, CommentData comment)
    {
        await InitializeAsync();
        if (_module == null) 
            throw new InvalidOperationException("Comments service not initialized");

        try
        {
            // Convert CommentData to anonymous object for JS interop
            var commentObj = new
            {
                commentId = comment.Id,
                author = comment.Author,
                content = comment.Content,
                timestamp = comment.CreatedAt,
                parentId = comment.ParentId,
                createdAt = comment.CreatedAt,
                url = comment.Url,
                score = comment.Score,
                metadata = comment.Metadata,
                replies = comment.Replies?.Select(r => new
                {
                    commentId = r.Id,
                    author = r.Author,
                    content = r.Content,
                    timestamp = r.CreatedAt,
                    parentId = r.ParentId,
                    createdAt = r.CreatedAt,
                    url = r.Url,
                    score = r.Score,
                    metadata = r.Metadata
                }).ToList()
            };

            var result = await _module.InvokeAsync<dynamic>("addComment", feedbackId, commentObj);
            
            // Convert result back to CommentData
            return ConvertToCommentData(result);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to add comment - service may not be initialized");
        }
    }

    public async Task<CommentData> EditCommentAsync(string commentId, CommentData updatedComment)
    {
        await InitializeAsync();
        if (_module == null) 
            throw new InvalidOperationException("Comments service not initialized");

        try
        {
            var commentObj = new
            {
                author = updatedComment.Author,
                content = updatedComment.Content,
                timestamp = updatedComment.CreatedAt,
                parentId = updatedComment.ParentId,
                url = updatedComment.Url,
                score = updatedComment.Score,
                metadata = updatedComment.Metadata
            };

            var result = await _module.InvokeAsync<dynamic>("editComment", commentId, commentObj);
            return ConvertToCommentData(result);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to edit comment - service may not be initialized");
        }
    }

    public async Task DeleteCommentAsync(string commentId)
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("deleteComment", commentId);
        }
        catch (InvalidOperationException)
        {
            // Service not initialized, comment probably doesn't exist anyway
        }
    }

    public async Task<List<CommentData>> GetCommentsByFeedbackIdAsync(string feedbackId)
    {
        await InitializeAsync();
        if (_module == null) return new List<CommentData>();

        try
        {
            var results = await _module.InvokeAsync<List<dynamic>>("getCommentsByFeedbackId", feedbackId);
            return results?.Select(ConvertToCommentData).ToList() ?? new List<CommentData>();
        }
        catch (InvalidOperationException)
        {
            return new List<CommentData>();
        }
    }

    public async Task<Dictionary<string, List<CommentData>>> GetCommentsByFeedbackIdsAsync(IEnumerable<string> feedbackIds)
    {
        await InitializeAsync();
        if (_module == null) return new Dictionary<string, List<CommentData>>();

        try
        {
            var feedbackIdsList = feedbackIds.ToList();
            if (!feedbackIdsList.Any())
                return new Dictionary<string, List<CommentData>>();

            var results = await _module.InvokeAsync<Dictionary<string, List<dynamic>>>("getCommentsByFeedbackIds", feedbackIdsList);
            
            var convertedResults = new Dictionary<string, List<CommentData>>();
            foreach (var kvp in results ?? new Dictionary<string, List<dynamic>>())
            {
                convertedResults[kvp.Key] = kvp.Value?.Select(ConvertToCommentData).ToList() ?? new List<CommentData>();
            }
            
            return convertedResults;
        }
        catch (InvalidOperationException)
        {
            return new Dictionary<string, List<CommentData>>();
        }
    }

    public async Task<int> DeleteCommentsByFeedbackIdAsync(string feedbackId)
    {
        await InitializeAsync();
        if (_module == null) return 0;

        try
        {
            return await _module.InvokeAsync<int>("deleteCommentsByFeedbackId", feedbackId);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public async Task ClearAllCommentsAsync()
    {
        await InitializeAsync();
        if (_module == null) return;

        try
        {
            await _module.InvokeVoidAsync("clearAllComments");
        }
        catch (InvalidOperationException)
        {
            // Service not initialized, nothing to clear
        }
    }

    public async Task<(int migrated, List<string> errors)> MigrateCommentsFromHistoryAsync(IEnumerable<AnalysisHistoryItem> historyItems)
    {
        await InitializeAsync();
        if (_module == null) 
            return (0, new List<string> { "Comments service not initialized" });

        try
        {
            // Convert history items to JS-compatible format
            var historyItemsObj = historyItems.Select(item => new
            {
                Id = item.Id,
                CommentThreads = item.CommentThreads?.Select(thread => new
                {
                    Comments = thread.Comments?.Select(comment => new
                    {
                        Author = comment.Author,
                        Content = comment.Content,
                        CreatedAt = comment.CreatedAt,
                        ParentId = comment.ParentId,
                        Url = comment.Url,
                        Score = comment.Score,
                        Metadata = comment.Metadata,
                        Replies = comment.Replies?.Select(reply => new
                        {
                            Author = reply.Author,
                            Content = reply.Content,
                            CreatedAt = reply.CreatedAt,
                            ParentId = reply.ParentId,
                            Url = reply.Url,
                            Score = reply.Score,
                            Metadata = reply.Metadata
                        }).ToList()
                    }).ToList()
                }).ToList()
            }).ToList();

            var result = await _module.InvokeAsync<dynamic>("migrateCommentsFromHistory", historyItemsObj);
            
            return (
                migrated: result.migrated ?? 0,
                errors: (result.errors as IEnumerable<object>)?.Select(e => e.ToString()).ToList() ?? new List<string>()
            );
        }
        catch (InvalidOperationException)
        {
            return (0, new List<string> { "Failed to migrate comments - service may not be initialized" });
        }
        catch (Exception ex)
        {
            return (0, new List<string> { $"Migration failed: {ex.Message}" });
        }
    }

    private static CommentData ConvertToCommentData(dynamic jsComment)
    {
        if (jsComment == null) return new CommentData();

        return new CommentData
        {
            Id = jsComment.commentId?.ToString() ?? string.Empty,
            ParentId = jsComment.parentId?.ToString(),
            Author = jsComment.author?.ToString() ?? string.Empty,
            Content = jsComment.content?.ToString() ?? string.Empty,
            CreatedAt = jsComment.createdAt != null ? DateTime.Parse(jsComment.createdAt.ToString()) : DateTime.MinValue,
            Url = jsComment.url?.ToString(),
            Score = jsComment.score != null ? Convert.ToInt32(jsComment.score) : null,
            Metadata = jsComment.metadata as Dictionary<string, object>,
            Replies = jsComment.replies != null 
                ? ((IEnumerable<dynamic>)jsComment.replies).Select(ConvertToCommentData).ToList()
                : new List<CommentData>()
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit is already disconnected, no need to dispose JS module
            }
        }

        GC.SuppressFinalize(this);
    }
}