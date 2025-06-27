using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedDump.Models.GitHub;
using System.Net;
using System.Text.Json;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubIssueCommentsTests
{
    [TestMethod]
    public async Task GetIssueCommentsAsync_ShouldRetrieveCommentsSuccessfully()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler(async (request, cancellationToken) =>
        {
            var jsonResponse = @"{
                ""data"": {
                    ""repository"": {
                        ""issue"": {
                            ""comments"": {
                                ""edges"": [
                                    {
                                        ""node"": {
                                            ""id"": ""comment_123"",
                                            ""body"": ""This is a test comment"",
                                            ""url"": ""https://github.com/owner/repo/issues/123#issuecomment-456"",
                                            ""createdAt"": ""2023-01-01T12:00:00Z"",
                                            ""author"": { ""login"": ""testuser"" }
                                        }
                                    },
                                    {
                                        ""node"": {
                                            ""id"": ""comment_124"",
                                            ""body"": ""This is another test comment"",
                                            ""url"": ""https://github.com/owner/repo/issues/123#issuecomment-457"",
                                            ""createdAt"": ""2023-01-01T13:00:00Z"",
                                            ""author"": { ""login"": ""anotheruser"" }
                                        }
                                    }
                                ],
                                ""pageInfo"": {
                                    ""hasNextPage"": false,
                                    ""endCursor"": null
                                }
                            }
                        }
                    }
                }
            }";

            return await Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });
        });

        var httpClient = new HttpClient(mockHttpHandler);
        var gitHubService = new GitHubService("fake-token", httpClient);

        // Act
        var comments = await gitHubService.GetIssueCommentsAsync("owner", "repo", 123);

        // Assert
        Assert.IsNotNull(comments);
        Assert.AreEqual(2, comments.Count);
        
        Assert.AreEqual("comment_123", comments[0].Id);
        Assert.AreEqual("This is a test comment", comments[0].Content);
        Assert.AreEqual("testuser", comments[0].Author);
        Assert.AreEqual("https://github.com/owner/repo/issues/123#issuecomment-456", comments[0].Url);
        Assert.AreEqual("2023-01-01T12:00:00Z", comments[0].CreatedAt);
        
        Assert.AreEqual("comment_124", comments[1].Id);
        Assert.AreEqual("This is another test comment", comments[1].Content);
        Assert.AreEqual("anotheruser", comments[1].Author);
        Assert.AreEqual("https://github.com/owner/repo/issues/123#issuecomment-457", comments[1].Url);
        Assert.AreEqual("2023-01-01T13:00:00Z", comments[1].CreatedAt);
    }

    [TestMethod]
    public async Task GetIssueCommentsAsync_ShouldReturnEmptyListWhenNoComments()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler(async (request, cancellationToken) =>
        {
            var jsonResponse = @"{
                ""data"": {
                    ""repository"": {
                        ""issue"": {
                            ""comments"": {
                                ""edges"": [],
                                ""pageInfo"": {
                                    ""hasNextPage"": false,
                                    ""endCursor"": null
                                }
                            }
                        }
                    }
                }
            }";

            return await Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });
        });

        var httpClient = new HttpClient(mockHttpHandler);
        var gitHubService = new GitHubService("fake-token", httpClient);

        // Act
        var comments = await gitHubService.GetIssueCommentsAsync("owner", "repo", 456);

        // Assert
        Assert.IsNotNull(comments);
        Assert.AreEqual(0, comments.Count);
    }
}
