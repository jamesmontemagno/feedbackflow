using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models;
using SharedDump.Models.YouTube;
using SharedDump.Services;
using System;
using System.Collections.Generic;

namespace FeedbackFlow.Tests
{
    [TestClass]
    public class CommentDataConverterTests
    {
        [TestMethod]
        public void ConvertYouTube_WithOrphanedComments_IncludesAllComments()
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
                        {
                            Id = "comment1",
                            ParentId = null,
                            Text = "Root comment",
                            Author = "User1",
                            PublishedAt = new DateTime(2025, 6, 1, 12, 0, 0)
                        },
                        // Reply to root comment
                        new YouTubeOutputComment
                        {
                            Id = "comment2",
                            ParentId = "comment1",
                            Text = "Reply to root",
                            Author = "User2",
                            PublishedAt = new DateTime(2025, 6, 1, 12, 30, 0)
                        },
                        // Orphaned comment (parent doesn't exist)
                        new YouTubeOutputComment
                        {
                            Id = "comment3",
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
            Assert.AreEqual(1, result.Count, "Should have one thread for the video");
            
            // Check we have two root comments (the actual root and the orphaned one)
            var rootComments = result[0].Comments;
            Assert.AreEqual(2, rootComments.Count, "Should have both the root comment and the orphaned comment");
            
            // Check the root comment has its reply
            var rootComment = rootComments.Find(c => c.Id == "comment1");
            Assert.IsNotNull(rootComment, "Root comment should be present");
            Assert.AreEqual(1, rootComment.Replies.Count, "Root comment should have one reply");
            Assert.AreEqual("comment2", rootComment.Replies[0].Id, "Reply should be properly linked");
            
            // Check the orphaned comment is included
            var orphanedComment = rootComments.Find(c => c.Id == "comment3");
            Assert.IsNotNull(orphanedComment, "Orphaned comment should be present at root level");
            Assert.IsTrue(orphanedComment.Content.StartsWith("[Reply to unavailable comment]"), 
                "Orphaned comment should be marked accordingly");
        }
    }
}
