using SharedDump.Models;
using SharedDump.Services;
using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Interfaces;
using NSubstitute;

namespace FeedbackFlow.Tests;

[TestClass]
public class ExportWithCommentsIntegrationTests
{
    [TestMethod]
    public async Task ExportDataService_PrepareItemsForExportAsync_WithComments_Success()
    {
        // Arrange
        var mockCommentsService = Substitute.For<ICommentsService>();
        
        // Set up mock data
        var comments = new List<CommentData>
        {
            new CommentData { Id = "comment-1", Author = "User1", Content = "Test comment" }
        };
        
        var commentsMap = new Dictionary<string, List<CommentData>>
        {
            { "feedback-1", comments }
        };
        
        mockCommentsService
            .GetCommentsByFeedbackIdsAsync(Arg.Any<IEnumerable<string>>())
            .Returns(commentsMap);
        
        var exportService = new ExportDataService(mockCommentsService);
        
        var historyItems = new List<AnalysisHistoryItem>
        {
            new AnalysisHistoryItem
            {
                Id = "feedback-1",
                Summary = "Test summary",
                SourceType = "Manual",
                Timestamp = DateTime.UtcNow
            }
        };
        
        // Act
        var result = await exportService.PrepareItemsForExportAsync(historyItems);
        
        // Assert
        Assert.IsNotNull(result);
        var resultList = result.ToList();
        Assert.AreEqual(1, resultList.Count);
        
        var itemWithComments = resultList[0];
        Assert.AreEqual("feedback-1", itemWithComments.Id);
        Assert.AreEqual("Test summary", itemWithComments.Summary);
        Assert.AreEqual(1, itemWithComments.CommentThreads.Count);
        Assert.AreEqual(1, itemWithComments.CommentThreads[0].Comments.Count);
        Assert.AreEqual("Test comment", itemWithComments.CommentThreads[0].Comments[0].Content);
    }
    
    [TestMethod]
    public async Task ExportService_ExportWithCommentsAsync_JsonFormat_Success()
    {
        // Arrange
        var exportService = new ExportService();
        
        var commentThreads = new List<CommentThread>
        {
            new CommentThread
            {
                Id = "thread-1",
                Comments = new List<CommentData>
                {
                    new CommentData { Id = "comment-1", Author = "User1", Content = "Test comment" }
                }
            }
        };
        
        var itemsWithComments = new List<AnalysisHistoryItemWithComments>
        {
            AnalysisHistoryItemWithComments.CreateFromHistoryItem(
                new AnalysisHistoryItem
                {
                    Id = "test-1",
                    Summary = "Test summary",
                    SourceType = "Manual",
                    Timestamp = DateTime.UtcNow
                },
                commentThreads
            )
        };
        
        // Debug: Check if comment threads are properly set
        var testItem = itemsWithComments.First();
        Assert.AreEqual(1, testItem.CommentThreads.Count);
        Assert.AreEqual(1, testItem.CommentThreads[0].Comments.Count);
        Assert.AreEqual("Test comment", testItem.CommentThreads[0].Comments[0].Content);
        
        // Act
        var result = await exportService.ExportWithCommentsAsync(itemsWithComments, ExportFormat.Json);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        
        // Convert to string to verify it's valid JSON
        result.Position = 0;
        using var reader = new StreamReader(result);
        var jsonContent = await reader.ReadToEndAsync();
        
        Assert.IsTrue(jsonContent.Contains("test-1"));
        Assert.IsTrue(jsonContent.Contains("Test summary"));
        Assert.IsTrue(jsonContent.Contains("Test comment"));
    }
}