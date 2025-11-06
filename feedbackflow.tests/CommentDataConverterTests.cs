using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models;
using SharedDump.Models.TwitterFeedback;
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
            Assert.HasCount(1, result, "Should have one thread for the video");
            
            // Check we have two root comments (the actual root and the orphaned one)
            var rootComments = result[0].Comments;
            Assert.HasCount(2, rootComments, "Should have both the root comment and the orphaned comment");
            
            // Check the root comment has its reply
            var rootComment = rootComments.Find(c => c.Id == "comment1");
            Assert.IsNotNull(rootComment, "Root comment should be present");
            Assert.HasCount(1, rootComment.Replies, "Root comment should have one reply");
            Assert.AreEqual("comment2", rootComment.Replies[0].Id, "Reply should be properly linked");
            
            // Check the orphaned comment is included
            var orphanedComment = rootComments.Find(c => c.Id == "comment3");
            Assert.IsNotNull(orphanedComment, "Orphaned comment should be present at root level");
            Assert.StartsWith("[Reply to unavailable comment]", orphanedComment.Content, 
                "Orphaned comment should be marked accordingly");
        }

        [TestMethod]
        public void ConvertTwitter_WithTweetAndReplies_ConvertsCorrectly()
        {
            // Arrange
            var twitterResponse = new TwitterFeedbackResponse
            {
                Items = new List<TwitterFeedbackItem>
                {
                    new TwitterFeedbackItem
                    {
                        Id = "123",
                        Author = "testuser",
                        AuthorName = "Test User",
                        AuthorUsername = "testuser",
                        Content = "This is a test tweet",
                        TimestampUtc = new DateTime(2025, 6, 1, 12, 0, 0),
                        ParentId = null,
                        Replies = new List<TwitterFeedbackItem>
                        {
                            new TwitterFeedbackItem
                            {
                                Id = "456",
                                Author = "replier",
                                AuthorName = "Replier",
                                AuthorUsername = "replier",
                                Content = "This is a reply",
                                TimestampUtc = new DateTime(2025, 6, 1, 12, 30, 0),
                                ParentId = "123"
                            }
                        }
                    }
                },
                ProcessedTweetCount = 2,
                MayBeIncomplete = false,
                RateLimitInfo = null
            };

            // Act
            var result = CommentDataConverter.ConvertTwitter(twitterResponse);

            // Assert
            Assert.HasCount(1, result, "Should have one thread for the tweet");
            
            var thread = result[0];
            Assert.AreEqual("123", thread.Id, "Thread ID should match tweet ID");
            Assert.AreEqual("Test User", thread.Author, "Thread author should be display name");
            Assert.AreEqual("Twitter", thread.SourceType, "Source type should be Twitter");
            Assert.AreEqual("This is a test tweet", thread.Description, "Thread description should match tweet content");
            
            // Check metadata
            Assert.IsTrue(thread?.Metadata?.ContainsKey("ProcessedTweetCount"), "Should include processed tweet count");
            Assert.AreEqual(2, thread?.Metadata?["ProcessedTweetCount"], "Processed tweet count should match");

            // Check comments (replies)
            Assert.HasCount(1, thread?.Comments ?? new List<CommentData>(), "Should have one reply");
            var reply = thread?.Comments?[0];
            Assert.AreEqual("456", reply?.Id, "Reply ID should match");
            Assert.AreEqual("Replier", reply?.Author, "Reply author should be display name");
            Assert.AreEqual("This is a reply", reply?.Content, "Reply content should match");
        }

        [TestMethod]
        public void ConvertAdditionalData_WithTwitterResponse_CallsTwitterConverter()
        {
            // Arrange
            var twitterResponse = new TwitterFeedbackResponse
            {
                Items = new List<TwitterFeedbackItem>
                {
                    new TwitterFeedbackItem
                    {
                        Id = "123",
                        Author = "testuser",
                        AuthorName = "Test User",
                        Content = "Test tweet",
                        TimestampUtc = DateTime.UtcNow,
                        ParentId = null
                    }
                },
                ProcessedTweetCount = 1
            };

            // Act
            var result = CommentDataConverter.ConvertAdditionalData(twitterResponse);

            // Assert
            Assert.HasCount(1, result, "Should convert TwitterFeedbackResponse");
            Assert.AreEqual("Twitter", result[0].SourceType, "Should have correct source type");
        }
    }
}
