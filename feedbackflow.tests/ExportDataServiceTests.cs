using Microsoft.JSInterop;
using NSubstitute;
using SharedDump.Models;
using FeedbackWebApp.Services;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackFlow.Tests;

[TestClass]
public class ExportDataServiceTests
{
    [TestMethod]
    public async Task PrepareItemsForExportAsync_EmptyList_ReturnsEmptyCollection()
    {
        // Arrange
        var mockCommentsService = Substitute.For<ICommentsService>();
        var exportDataService = new ExportDataService(mockCommentsService);
        var historyItems = new List<AnalysisHistoryItem>();

        // Act
        var result = await exportDataService.PrepareItemsForExportAsync(historyItems);

        // Assert
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task PrepareItemsForExportAsync_WithHistoryItems_CombinesWithComments()
    {
        // Arrange
        var mockCommentsService = Substitute.For<ICommentsService>();
        var exportDataService = new ExportDataService(mockCommentsService);
        
        var historyItems = new List<AnalysisHistoryItem>
        {
            new AnalysisHistoryItem 
            { 
                Id = "history-1", 
                Summary = "Test Summary",
                SourceType = "YouTube"
            },
            new AnalysisHistoryItem 
            { 
                Id = "history-2", 
                Summary = "Test Summary 2",
                SourceType = "Reddit"
            }
        };

        var commentsMap = new Dictionary<string, List<CommentData>>
        {
            ["history-1"] = new List<CommentData>
            {
                new CommentData { Id = "comment-1", Author = "User1", Content = "Great video!" }
            },
            ["history-2"] = new List<CommentData>() // No comments
        };

        mockCommentsService
            .GetCommentsByFeedbackIdsAsync(Arg.Is<IEnumerable<string>>(ids => 
                ids.Contains("history-1") && ids.Contains("history-2")))
            .Returns(commentsMap);

        // Act
        var result = await exportDataService.PrepareItemsForExportAsync(historyItems);

        // Assert
        var resultList = result.ToList();
        Assert.AreEqual(2, resultList.Count);
        
        // Check first item has comments
        var firstItem = resultList.First(r => r.Id == "history-1");
        Assert.AreEqual(1, firstItem.CommentThreads.Count);
        Assert.AreEqual(1, firstItem.CommentThreads[0].Comments.Count);
        Assert.AreEqual("Great video!", firstItem.CommentThreads[0].Comments[0].Content);
        
        // Check second item has no comments
        var secondItem = resultList.First(r => r.Id == "history-2");
        Assert.AreEqual(0, secondItem.CommentThreads.Count);
        
        // Verify comments service was called
        await mockCommentsService.Received(1)
            .GetCommentsByFeedbackIdsAsync(Arg.Any<IEnumerable<string>>());
    }

    [TestMethod]
    public async Task PrepareItemsForExportAsync_SingleItem_CreatesCommentThread()
    {
        // Arrange
        var mockCommentsService = Substitute.For<ICommentsService>();
        var exportDataService = new ExportDataService(mockCommentsService);
        
        var historyItem = new AnalysisHistoryItem 
        { 
            Id = "test-id", 
            Summary = "Test Summary",
            SourceType = "GitHub",
            Timestamp = DateTime.UtcNow
        };

        var comments = new List<CommentData>
        {
            new CommentData 
            { 
                Id = "comment-1", 
                Author = "Developer", 
                Content = "This looks good!",
                CreatedAt = DateTime.UtcNow
            }
        };

        var commentsMap = new Dictionary<string, List<CommentData>>
        {
            ["test-id"] = comments
        };

        mockCommentsService
            .GetCommentsByFeedbackIdsAsync(Arg.Any<IEnumerable<string>>())
            .Returns(commentsMap);

        // Act
        var result = await exportDataService.PrepareItemsForExportAsync(new[] { historyItem });

        // Assert
        var resultList = result.ToList();
        Assert.AreEqual(1, resultList.Count);
        
        var exportItem = resultList[0];
        Assert.AreEqual("test-id", exportItem.Id);
        Assert.AreEqual("Test Summary", exportItem.Summary);
        Assert.AreEqual("GitHub", exportItem.SourceType);
        
        // Check comment thread structure
        Assert.AreEqual(1, exportItem.CommentThreads.Count);
        var thread = exportItem.CommentThreads[0];
        Assert.AreEqual("test-id", thread.Id);
        Assert.AreEqual("GitHub Analysis", thread.Title);
        Assert.AreEqual("Test Summary", thread.Description);
        Assert.AreEqual("GitHub", thread.SourceType);
        
        // Check comments
        Assert.AreEqual(1, thread.Comments.Count);
        var comment = thread.Comments[0];
        Assert.AreEqual("comment-1", comment.Id);
        Assert.AreEqual("Developer", comment.Author);
        Assert.AreEqual("This looks good!", comment.Content);
    }
}