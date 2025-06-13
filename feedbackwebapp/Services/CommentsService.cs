using Microsoft.JSInterop;
using SharedDump.Models;
using FeedbackWebApp.Services.Interfaces;
using System.Reflection;

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
                errors: (result.errors as IEnumerable<object>)?.Select(e => e?.ToString() ?? string.Empty).ToList() ?? new List<string>()
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
            Id = GetSafeStringValue(jsComment, "commentId") ?? string.Empty,
            ParentId = GetSafeStringValue(jsComment, "parentId"),
            Author = GetSafeStringValue(jsComment, "author") ?? string.Empty,
            Content = GetSafeStringValue(jsComment, "content") ?? string.Empty,
            CreatedAt = GetSafeDateTimeValue(jsComment, "createdAt") ?? DateTime.MinValue,
            Url = GetSafeStringValue(jsComment, "url"),
            Score = GetSafeIntValue(jsComment, "score"),
            Metadata = GetSafeMetadataValue(jsComment, "metadata"),
            Replies = GetSafeRepliesValue(jsComment, "replies")
        };
    }

    private static string? GetSafeStringValue(dynamic obj, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(obj, propertyName);
            if (value == null) return null;
            
            var stringValue = value.ToString();
            return string.IsNullOrEmpty(stringValue) ? null : stringValue;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? GetSafeDateTimeValue(dynamic obj, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(obj, propertyName);
            if (value == null) return null;
            
            var stringValue = value.ToString();
            if (string.IsNullOrEmpty(stringValue)) return null;
            
            return DateTime.TryParse(stringValue, out DateTime result) ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? GetSafeIntValue(dynamic obj, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(obj, propertyName);
            if (value == null) return null;
            
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            
            var stringValue = value.ToString();
            if (string.IsNullOrEmpty(stringValue)) return null;
            
            return int.TryParse(stringValue, out int result) ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object>? GetSafeMetadataValue(dynamic obj, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(obj, propertyName);
            return value as Dictionary<string, object>;
        }
        catch
        {
            return null;
        }
    }

    private static List<CommentData> GetSafeRepliesValue(dynamic obj, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(obj, propertyName);
            if (value == null) return new List<CommentData>();
            
            if (value is IEnumerable<dynamic> enumerable)
            {
                return enumerable.Select(ConvertToCommentData).ToList();
            }
            
            return new List<CommentData>();
        }
        catch
        {
            return new List<CommentData>();
        }
    }

    private static object? GetPropertyValue(dynamic obj, string propertyName)
    {
        try
        {
            // Handle JsonElement case
            if (obj is System.Text.Json.JsonElement element)
            {
                if (element.TryGetProperty(propertyName, out var property))
                {
                    return property.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => property.GetString(),
                        System.Text.Json.JsonValueKind.Number => property.TryGetInt32(out var intVal) ? intVal : property.GetDouble(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.Null => null,
                        System.Text.Json.JsonValueKind.Undefined => null,
                        _ => property.GetRawText()
                    };
                }
                return null;
            }
            
            // Handle other dynamic types
            var type = obj.GetType();
            var prop = type.GetProperty(propertyName);
            return prop?.GetValue(obj);
        }
        catch
        {
            return null;
        }
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