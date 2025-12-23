using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models;
using SharedDump.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FeedbackFlow.Tests;

[TestClass]
public class CommentMinifierTests
{
    [TestMethod]
    public void MinifyThread_WithBasicData_CreatesMinifiedVersion()
    {
        // Arrange
        var thread = new CommentThread
        {
            Id = "thread1",
            Title = "Test Thread",
            Description = "Test Description",
            Author = "TestAuthor",
            CreatedAt = new DateTime(2025, 6, 1, 12, 0, 0),
            SourceType = "GitHub",
            Comments = new List<CommentData>
            {
                new CommentData
                {
                    Id = "comment1",
                    Author = "User1",
                    Content = "Test comment",
                    CreatedAt = new DateTime(2025, 6, 1, 13, 0, 0),
                    Score = 5
                }
            }
        };

        // Act
        var minified = CommentMinifier.MinifyThread(thread);

        // Assert
        Assert.AreEqual(thread.Title, minified.Title);
        Assert.AreEqual(thread.Description, minified.Description);
        Assert.AreEqual(thread.Author, minified.Author);
        Assert.AreEqual(thread.CreatedAt, minified.CreatedAt);
        Assert.AreEqual(thread.SourceType, minified.Platform);
        Assert.AreEqual(1, minified.Comments.Count);
        Assert.AreEqual("User1", minified.Comments[0].Author);
        Assert.AreEqual("Test comment", minified.Comments[0].Content);
        Assert.AreEqual(5, minified.Comments[0].Score);
    }

    [TestMethod]
    public void MinifyThread_WithNestedReplies_PreservesHierarchy()
    {
        // Arrange
        var thread = new CommentThread
        {
            Id = "thread1",
            Title = "Test Thread",
            Author = "TestAuthor",
            CreatedAt = DateTime.UtcNow,
            SourceType = "Reddit",
            Comments = new List<CommentData>
            {
                new CommentData
                {
                    Id = "comment1",
                    Author = "User1",
                    Content = "Root comment",
                    CreatedAt = DateTime.UtcNow,
                    Replies = new List<CommentData>
                    {
                        new CommentData
                        {
                            Id = "reply1",
                            ParentId = "comment1",
                            Author = "User2",
                            Content = "Reply to root",
                            CreatedAt = DateTime.UtcNow
                        }
                    }
                }
            }
        };

        // Act
        var minified = CommentMinifier.MinifyThread(thread);

        // Assert
        Assert.AreEqual(1, minified.Comments.Count);
        Assert.AreEqual(1, minified.Comments[0].Replies.Count);
        Assert.AreEqual("User2", minified.Comments[0].Replies[0].Author);
        Assert.AreEqual("Reply to root", minified.Comments[0].Replies[0].Content);
    }

    [TestMethod]
    public void MinifyThread_ExcludesMetadata_ReducesPayloadSize()
    {
        // Arrange
        var thread = new CommentThread
        {
            Id = "thread1",
            Title = "Test Thread",
            Author = "TestAuthor",
            CreatedAt = DateTime.UtcNow,
            SourceType = "GitHub",
            Url = "https://github.com/test/repo/issues/123",
            Metadata = new Dictionary<string, object>
            {
                ["Upvotes"] = 100,
                ["Labels"] = new List<string> { "bug", "enhancement" },
                ["LastUpdated"] = DateTime.UtcNow
            },
            Comments = new List<CommentData>
            {
                new CommentData
                {
                    Id = "comment1",
                    ParentId = null,
                    Author = "User1",
                    Content = "Test comment",
                    CreatedAt = DateTime.UtcNow,
                    Url = "https://github.com/test/repo/issues/123#comment1",
                    Metadata = new Dictionary<string, object>
                    {
                        ["CodeContext"] = "someCode()",
                        ["FilePath"] = "/path/to/file.cs"
                    }
                }
            }
        };

        // Act
        var minified = CommentMinifier.MinifyThread(thread);
        
        // Serialize both to compare sizes
        var fullJson = JsonSerializer.Serialize(thread);
        var minifiedJson = JsonSerializer.Serialize(minified);

        // Assert
        Assert.IsTrue(minifiedJson.Length < fullJson.Length, 
            $"Minified JSON ({minifiedJson.Length} bytes) should be smaller than full JSON ({fullJson.Length} bytes)");
        
        // Verify essential data is preserved
        Assert.AreEqual(thread.Title, minified.Title);
        Assert.AreEqual(thread.Author, minified.Author);
        Assert.AreEqual(1, minified.Comments.Count);
        Assert.AreEqual("User1", minified.Comments[0].Author);
        Assert.AreEqual("Test comment", minified.Comments[0].Content);
    }

    [TestMethod]
    public void ConvertMinifiedThreadsToText_GeneratesCorrectFormat()
    {
        // Arrange
        var minifiedThreads = new List<MinifiedCommentThread>
        {
            new MinifiedCommentThread
            {
                Title = "Test Issue",
                Description = "Issue description",
                Author = "IssueAuthor",
                CreatedAt = new DateTime(2025, 6, 1, 12, 0, 0),
                Platform = "GitHub",
                Comments = new List<MinifiedCommentData>
                {
                    new MinifiedCommentData
                    {
                        Author = "Commenter1",
                        Content = "Great issue!",
                        CreatedAt = new DateTime(2025, 6, 1, 13, 0, 0),
                        Score = 5
                    }
                }
            }
        };

        // Act
        var text = CommentMinifier.ConvertMinifiedThreadsToText(minifiedThreads);

        // Assert
        Assert.IsTrue(text.Contains("# Test Issue"));
        Assert.IsTrue(text.Contains("Description: Issue description"));
        Assert.IsTrue(text.Contains("Author: IssueAuthor"));
        Assert.IsTrue(text.Contains("Source: GitHub"));
        Assert.IsTrue(text.Contains("**Commenter1**"));
        Assert.IsTrue(text.Contains("Great issue!"));
        Assert.IsTrue(text.Contains("Score: 5"));
    }

    [TestMethod]
    public void MinifyThreads_WithMultipleThreads_ProcessesAll()
    {
        // Arrange
        var threads = new List<CommentThread>
        {
            new CommentThread
            {
                Id = "thread1",
                Title = "Thread 1",
                Author = "Author1",
                CreatedAt = DateTime.UtcNow,
                SourceType = "GitHub"
            },
            new CommentThread
            {
                Id = "thread2",
                Title = "Thread 2",
                Author = "Author2",
                CreatedAt = DateTime.UtcNow,
                SourceType = "Reddit"
            }
        };

        // Act
        var minified = CommentMinifier.MinifyThreads(threads);

        // Assert
        Assert.AreEqual(2, minified.Count);
        Assert.AreEqual("Thread 1", minified[0].Title);
        Assert.AreEqual("Thread 2", minified[1].Title);
        Assert.AreEqual("GitHub", minified[0].Platform);
        Assert.AreEqual("Reddit", minified[1].Platform);
    }

    [TestMethod]
    public void MinifyThread_WithEmptyComments_HandlesGracefully()
    {
        // Arrange
        var thread = new CommentThread
        {
            Id = "thread1",
            Title = "Empty Thread",
            Author = "Author",
            CreatedAt = DateTime.UtcNow,
            SourceType = "YouTube",
            Comments = new List<CommentData>()
        };

        // Act
        var minified = CommentMinifier.MinifyThread(thread);

        // Assert
        Assert.AreEqual(0, minified.Comments.Count);
        Assert.AreEqual(thread.Title, minified.Title);
    }
}
