using Microsoft.JSInterop;
using NSubstitute;
using SharedDump.Models;
using FeedbackWebApp.Services;

namespace FeedbackFlow.Tests;

[TestClass]
public class CommentsServiceTests
{
    [TestMethod]
    public async Task CommentsService_Dispose_DoesNotThrow()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        var service = new CommentsService(mockJSRuntime);

        // Act & Assert - should not throw
        await service.DisposeAsync();
    }

    [TestMethod] 
    public async Task GetCommentsByFeedbackIdAsync_ServiceNotInitialized_ReturnsEmptyList()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);

        // Act
        var result = await service.GetCommentsByFeedbackIdAsync("any-id");

        // Assert
        Assert.AreEqual(0, result.Count);
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task GetCommentsByFeedbackIdsAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);

        // Act
        var result = await service.GetCommentsByFeedbackIdsAsync(new List<string>());

        // Assert
        Assert.AreEqual(0, result.Count);
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task DeleteCommentsByFeedbackIdAsync_ServiceNotInitialized_ReturnsZero()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);

        // Act
        var result = await service.DeleteCommentsByFeedbackIdAsync("any-id");

        // Assert
        Assert.AreEqual(0, result);
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task ClearAllCommentsAsync_ServiceNotInitialized_DoesNotThrow()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);

        // Act & Assert - should not throw
        await service.ClearAllCommentsAsync();
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task DeleteCommentAsync_ServiceNotInitialized_DoesNotThrow()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);

        // Act & Assert - should not throw
        await service.DeleteCommentAsync("any-id");
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task AddCommentAsync_ServiceNotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);
        var comment = new CommentData { Author = "Test", Content = "Test content" };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.AddCommentAsync("feedback-id", comment)
        );
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task EditCommentAsync_ServiceNotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);
        var comment = new CommentData { Author = "Test", Content = "Test content" };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.EditCommentAsync("comment-id", comment)
        );
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task MigrateCommentsFromHistoryAsync_ServiceNotInitialized_ReturnsErrorResult()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));

        var service = new CommentsService(mockJSRuntime);
        var historyItems = new List<AnalysisHistoryItem>
        {
            new AnalysisHistoryItem
            {
                Id = "test-id",
                CommentThreads = new List<CommentThread>
                {
                    new CommentThread
                    {
                        Comments = new List<CommentData>
                        {
                            new CommentData { Author = "Test", Content = "Test content" }
                        }
                    }
                }
            }
        };

        // Act
        var result = await service.MigrateCommentsFromHistoryAsync(historyItems);

        // Assert
        Assert.AreEqual(0, result.migrated);
        Assert.IsTrue(result.errors.Count > 0);
        Assert.IsTrue(result.errors[0].Contains("Comments service not initialized"));
        
        // Cleanup
        await service.DisposeAsync();
    }
}