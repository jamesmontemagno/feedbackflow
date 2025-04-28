using SharedDump.Models.GitHub;
using System.Net;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubPullRequestCommentsTests
{
    [TestMethod]
    public async Task GetPullRequestCommentsAsync_ShouldRetrieveAllCommentTypes()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler(async (request, cancellationToken) =>
        {
            var jsonResponse = @"{
                ""data"": {
                    ""repository"": {
                        ""pullRequest"": {
                            ""id"": ""PR_123"",
                            ""author"": { ""login"": ""testuser"" },
                            ""title"": ""Test PR"",
                            ""body"": ""PR description"",
                            ""url"": ""https://github.com/repo/pr/123"",
                            ""createdAt"": ""2023-01-01T00:00:00Z"",
                            ""updatedAt"": ""2023-01-02T00:00:00Z"",
                            ""reactions"": { ""totalCount"": 5 },
                            ""labels"": { ""nodes"": [{ ""name"": ""bug"" }] },
                            ""comments"": {
                                ""edges"": [
                                    {
                                        ""node"": {
                                            ""id"": ""comment1"",
                                            ""body"": ""Regular comment"",
                                            ""url"": ""https://github.com/comment1"",
                                            ""createdAt"": ""2023-01-01T12:00:00Z"",
                                            ""author"": { ""login"": ""commenter"" }
                                        }
                                    }
                                ],
                                ""pageInfo"": {
                                    ""hasNextPage"": false,
                                    ""endCursor"": null
                                }
                            },
                            ""reviews"": {
                                ""nodes"": [
                                    {
                                        ""author"": { ""login"": ""reviewer"" },
                                        ""body"": ""Review comment"",
                                        ""comments"": {
                                            ""nodes"": [
                                                {
                                                    ""id"": ""review1"",
                                                    ""body"": ""Code review comment"",
                                                    ""path"": ""src/file.cs"",
                                                    ""position"": 10,
                                                    ""diffHunk"": ""@@ -1,5 +1,5 @@\\n function test() {\\n-  return true;\\n+  return false;\\n }\\n"",
                                                    ""url"": ""https://github.com/review1"",
                                                    ""createdAt"": ""2023-01-01T13:00:00Z"",
                                                    ""author"": { ""login"": ""reviewer"" }
                                                }
                                            ]
                                        }
                                    }
                                ]
                            }
                        }
                    }
                }
            }";

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            };
        });

        var httpClient = new HttpClient(mockHttpHandler);
        var gitHubService = new GitHubService("fake-token", httpClient);

        // Act
        var comments = await gitHubService.GetPullRequestCommentsAsync("owner", "repo", 123);

        // Assert
        Assert.AreEqual(2, comments.Count, "Should have two comments (one regular, one review)");
        
        // Check regular comment
        var regularComment = comments.FirstOrDefault(c => c.Id == "comment1");
        Assert.IsNotNull(regularComment, "Regular comment should exist");
        Assert.AreEqual("Regular comment", regularComment.Content);
        Assert.AreEqual("commenter", regularComment.Author);
        
        // Check code review comment
        var reviewComment = comments.FirstOrDefault(c => c.Id == "review1");
        Assert.IsNotNull(reviewComment, "Review comment should exist");
        Assert.AreEqual("Code review comment", reviewComment.Content);
        Assert.AreEqual("reviewer", reviewComment.Author);
        Assert.AreEqual("src/file.cs", reviewComment.FilePath);
        Assert.AreEqual(10, reviewComment.LinePosition);
        Assert.IsNotNull(reviewComment.CodeContext, "CodeContext should be populated");
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
