using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models;
using SharedDump.Services;
using SharedDump.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace FeedbackFlow.Tests;

[TestClass]
public class ExportServiceTests
{
    private IExportService _exportService = null!;
    private List<AnalysisHistoryItem> _testItems = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _exportService = new ExportService();
        _testItems = new List<AnalysisHistoryItem>
        {            new AnalysisHistoryItem
            {
                Id = "test-1",
                Timestamp = new DateTime(2025, 1, 15, 10, 30, 0),
                FullAnalysis = "Test summary 1", // Set FullAnalysis to be the same as previous Summary for tests
                SourceType = "Manual",
                UserInput = "Test input",
                IsShared = false
            },            new AnalysisHistoryItem
            {
                Id = "test-2",
                Timestamp = new DateTime(2025, 1, 16, 14, 15, 0),
                FullAnalysis = "Test summary 2", // Set FullAnalysis to be the same as previous Summary for tests
                SourceType = "GitHub",
                UserInput = "https://github.com/test/repo",
                IsShared = true,
                SharedId = "shared-123",
                SharedDate = new DateTime(2025, 1, 16, 15, 0, 0)
            }
        };
    }

    [TestMethod]
    public async Task ExportAsync_CsvFormat_ReturnsValidCsvStream()
    {
        // Act
        var result = await _exportService.ExportAsync(_testItems, ExportFormat.Csv);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        
        result.Position = 0;
        var content = await new StreamReader(result).ReadToEndAsync();
        
        Assert.IsTrue(content.Contains("Id,Date,Source Type"));
        Assert.IsTrue(content.Contains("test-1"));
        Assert.IsTrue(content.Contains("Manual"));
        Assert.IsTrue(content.Contains("Test summary 1"));
    }

    [TestMethod]
    public async Task ExportAsync_JsonFormat_ReturnsValidJsonStream()
    {
        // Act
        var result = await _exportService.ExportAsync(_testItems, ExportFormat.Json);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        
        result.Position = 0;
        var content = await new StreamReader(result).ReadToEndAsync();
        
        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(content);
        Assert.IsNotNull(jsonDoc);
        
        var root = jsonDoc.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(2, root.GetArrayLength());
    }

    [TestMethod]
    public async Task ExportAsync_MarkdownFormat_ReturnsValidMarkdownStream()
    {
        // Act
        var result = await _exportService.ExportAsync(_testItems, ExportFormat.Markdown);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        
        result.Position = 0;
        var content = await new StreamReader(result).ReadToEndAsync();
        
        Assert.IsTrue(content.Contains("# Analysis Export"));
        Assert.IsTrue(content.Contains("## Analysis - Manual"));
        Assert.IsTrue(content.Contains("## Analysis - GitHub"));
        Assert.IsTrue(content.Contains("### Summary"));
        Assert.IsTrue(content.Contains("Test summary 1"));
    }

    [TestMethod]
    public async Task ExportAsync_PdfFormat_ReturnsValidPdfStream()
    {
        // Act
        var result = await _exportService.ExportAsync(_testItems, ExportFormat.Pdf);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        
        // Check that it starts with PDF header
        result.Position = 0;
        var buffer = new byte[4];
        await result.ReadAsync(buffer, 0, 4);
        var header = Encoding.ASCII.GetString(buffer);
        Assert.AreEqual("%PDF", header);
    }

    [TestMethod]
    public void GetFileExtension_ReturnsCorrectExtensions()
    {
        Assert.AreEqual(".csv", _exportService.GetFileExtension(ExportFormat.Csv));
        Assert.AreEqual(".json", _exportService.GetFileExtension(ExportFormat.Json));
        Assert.AreEqual(".pdf", _exportService.GetFileExtension(ExportFormat.Pdf));
        Assert.AreEqual(".md", _exportService.GetFileExtension(ExportFormat.Markdown));
    }

    [TestMethod]
    public void GetMimeType_ReturnsCorrectMimeTypes()
    {
        Assert.AreEqual("text/csv", _exportService.GetMimeType(ExportFormat.Csv));
        Assert.AreEqual("application/json", _exportService.GetMimeType(ExportFormat.Json));
        Assert.AreEqual("application/pdf", _exportService.GetMimeType(ExportFormat.Pdf));
        Assert.AreEqual("text/markdown", _exportService.GetMimeType(ExportFormat.Markdown));
    }

    [TestMethod]
    public async Task ExportAsync_UnsupportedFormat_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _exportService.ExportAsync(_testItems, (ExportFormat)999));
    }

    [TestMethod]
    public async Task ExportAsync_EmptyItemsList_ReturnsValidStream()
    {
        // Arrange
        var emptyItems = new List<AnalysisHistoryItem>();
        
        // Act
        var result = await _exportService.ExportAsync(emptyItems, ExportFormat.Csv);
        
        // Assert
        Assert.IsNotNull(result);
        
        result.Position = 0;
        var content = await new StreamReader(result).ReadToEndAsync();
        
        // Should still have headers
        Assert.IsTrue(content.Contains("Id,Date,Source Type"));
    }

    [TestMethod]
    public async Task ExportAsync_ItemsWithCommentThreads_IncludesCommentData()
    {
        // Arrange
        var itemsWithComments = new List<AnalysisHistoryItem>
        {            new AnalysisHistoryItem
            {
                Id = "test-with-comments",
                Timestamp = new DateTime(2025, 1, 17, 9, 0, 0),
                FullAnalysis = "Analysis with comments", // Set FullAnalysis to be the same as previous Summary for tests
                SourceType = "Reddit",
                UserInput = "https://reddit.com/r/test/comments/123",
                CommentThreads = new List<CommentThread>
                {
                    new CommentThread
                    {
                        Id = "thread-1",
                        Title = "Test Thread",
                        Author = "thread_author",
                        CreatedAt = new DateTime(2025, 1, 17, 8, 0, 0),
                        Url = "https://reddit.com/r/test/comments/123",
                        SourceType = "Reddit",
                        Comments = new List<CommentData>
                        {
                            new CommentData
                            {
                                Id = "comment-1",
                                Author = "user1",
                                Content = "This is a test comment",
                                CreatedAt = new DateTime(2025, 1, 17, 8, 30, 0),
                                Score = 5,
                                Replies = new List<CommentData>
                                {
                                    new CommentData
                                    {
                                        Id = "comment-2",
                                        ParentId = "comment-1",
                                        Author = "user2",
                                        Content = "This is a reply",
                                        CreatedAt = new DateTime(2025, 1, 17, 8, 45, 0),
                                        Score = 2
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Test CSV export includes comment data
        var csvResult = await _exportService.ExportAsync(itemsWithComments, ExportFormat.Csv);
        csvResult.Position = 0;
        var csvContent = await new StreamReader(csvResult).ReadToEndAsync();
        
        Assert.IsTrue(csvContent.Contains("Comment Threads Count"));
        Assert.IsTrue(csvContent.Contains("1")); // Comment thread count
        Assert.IsTrue(csvContent.Contains("Thread Id"));
        Assert.IsTrue(csvContent.Contains("Test Thread"));
        Assert.IsTrue(csvContent.Contains("This is a test comment"));
        Assert.IsTrue(csvContent.Contains("This is a reply"));

        // Test JSON export includes comment data
        var jsonResult = await _exportService.ExportAsync(itemsWithComments, ExportFormat.Json);
        jsonResult.Position = 0;
        var jsonContent = await new StreamReader(jsonResult).ReadToEndAsync();
        
        var jsonDoc = JsonDocument.Parse(jsonContent);
        var firstItem = jsonDoc.RootElement[0];
        
        Assert.IsTrue(firstItem.TryGetProperty("commentThreads", out var commentThreads));
        Assert.AreEqual(1, commentThreads.GetArrayLength());
        
        var firstThread = commentThreads[0];
        Assert.IsTrue(firstThread.TryGetProperty("title", out var title));
        Assert.AreEqual("Test Thread", title.GetString());

        // Test Markdown export includes comment data
        var mdResult = await _exportService.ExportAsync(itemsWithComments, ExportFormat.Markdown);
        mdResult.Position = 0;
        var mdContent = await new StreamReader(mdResult).ReadToEndAsync();
        
        Assert.IsTrue(mdContent.Contains("### Comment Threads"));
        Assert.IsTrue(mdContent.Contains("#### Test Thread"));
        Assert.IsTrue(mdContent.Contains("**user1**"));
        Assert.IsTrue(mdContent.Contains("This is a test comment"));
        Assert.IsTrue(mdContent.Contains("user2")); // Check for reply author (without specific indentation)

        // Test PDF export includes comment data
        var pdfResult = await _exportService.ExportAsync(itemsWithComments, ExportFormat.Pdf);
        Assert.IsNotNull(pdfResult);
        Assert.IsTrue(pdfResult.Length > 0);
    }
}