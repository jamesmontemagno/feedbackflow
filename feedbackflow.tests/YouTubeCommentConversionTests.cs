using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models;
using SharedDump.Models.YouTube;
using SharedDump.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeedbackFlow.Tests
{
    [TestClass]
    public class YouTubeCommentConversionTests
    {
        [TestMethod]
        public void ConvertYouTube_OrphanedComments_ShouldBeIncludedInResult()
        {
            // Arrange
            var videos = new List<YouTubeOutputVideo>
            {
                new YouTubeOutputVideo
                {
                    Id = "video1",
                    Title = "Test Video",
                    Comments = new List<YouTubeOutputComment>
                    {
                        // Root comment
                        new YouTubeOutputComment
                        {                            Id = "comment1", 
                            ParentId = null,
                            Text = "Root comment",
                            Author = "User1",
                            PublishedAt = new DateTime(2025, 6, 1, 12, 0, 0)
                        },
                        // Reply to root comment
                        new YouTubeOutputComment
                        {                            Id = "comment2",
                            ParentId = "comment1",
                            Text = "Reply to root",
                            Author = "User2",
                            PublishedAt = new DateTime(2025, 6, 1, 12, 30, 0)
                        },
                        // Orphaned comment (parent doesn't exist)
                        new YouTubeOutputComment
                        {                            Id = "comment3",
                            ParentId = "non-existent",
                            Text = "Orphaned comment",
                            Author = "User3",
                            PublishedAt = new DateTime(2025, 6, 1, 13, 0, 0)
                        }
                    }
                }
            };

            // Act
            var result = CommentDataConverter.ConvertYouTube(videos);

            // Assert
            Assert.HasCount(1, result, "Should convert one video thread");
            
            var comments = result[0].Comments;
            
            // Should include both root comments and orphaned comments at the top level
            Assert.HasCount(2, comments, "Should include both root and orphaned comments");
            
            // Verify the orphaned comment was included
            var orphanedComment = comments.FirstOrDefault(c => c.Content.StartsWith("[Reply to unavailable comment]"));
            Assert.IsNotNull(orphanedComment, "Should find the orphaned comment with the prefix");
            Assert.Contains("Orphaned comment", orphanedComment.Content, "Content of the orphaned comment should be preserved");
            
            // Verify the root comment has its reply intact
            var rootComment = comments.FirstOrDefault(c => c.Id == "comment1");
            Assert.IsNotNull(rootComment, "Should find the root comment");
            Assert.HasCount(1, rootComment.Replies, "Root comment should have one reply");
            Assert.AreEqual("comment2", rootComment.Replies[0].Id, "The reply should be correctly linked");
        }
    }
}
