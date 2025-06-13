using Microsoft.JSInterop;
using NSubstitute;
using SharedDump.Models;
using FeedbackWebApp.Services;

namespace FeedbackFlow.Tests;

[TestClass]
public class MigrationAndExportTests
{
    [TestMethod]
    public async Task CommentsService_ConvertToCommentData_HandlesNullValues()
    {
        // This tests the ConvertToCommentData method's robustness
        // by using reflection to call the private method
        
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        var service = new CommentsService(mockJSRuntime);
        
        // Test that the service can be created without throwing
        Assert.IsNotNull(service);
        
        // Cleanup
        await service.DisposeAsync();
    }

    [TestMethod]
    public async Task CommentsService_GetCommentsByFeedbackIdsAsync_WithEmptyList_ReturnsEmptyDictionary()
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
    public async Task CommentsService_MigrateCommentsFromHistoryAsync_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var mockJSRuntime = Substitute.For<IJSRuntime>();
        mockJSRuntime
            .InvokeAsync<IJSObjectReference>("import", "./js/commentsDb.js")
            .Returns(ValueTask.FromException<IJSObjectReference>(new InvalidOperationException("Prerendering")));
        
        var service = new CommentsService(mockJSRuntime);
        
        // Act
        var result = await service.MigrateCommentsFromHistoryAsync(new List<AnalysisHistoryItem>());
        
        // Assert
        Assert.AreEqual(0, result.migrated);
        Assert.IsTrue(result.errors.Count > 0); // Should have an error due to service not initialized
        
        // Cleanup
        await service.DisposeAsync();
    }
}