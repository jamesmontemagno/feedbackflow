using System.Net;
using SharedDump.Models.GitHub;

namespace FeedbackFlow.Tests;

[TestClass]
public class GitHubDiscussionServiceTests
{
    [TestMethod]
    public async Task GetOrganizationDiscussionWithCommentsAsync_SearchesScopedDiscussion_ReturnsComments()
    {
        var handler = new CapturingGitHubMessageHandler("""
            {
              "data": {
                "search": {
                  "nodes": [
                    {
                      "id": "D_kwDOEfmk4M4Amsd1",
                      "number": 197089,
                      "title": "All GitHub Copilot plans are now on usage-based billing [FAQ]",
                      "url": "https://github.com/orgs/community/discussions/197089",
                      "answer": { "id": "answer-1" },
                      "comments": {
                        "edges": [
                          {
                            "node": {
                              "id": "comment-1",
                              "body": "First comment",
                              "url": "https://github.com/orgs/community/discussions/197089#discussioncomment-1",
                              "createdAt": "2026-01-01T00:00:00Z",
                              "author": { "login": "commenter" },
                              "replies": {
                                "edges": [
                                  {
                                    "node": {
                                      "id": "reply-1",
                                      "body": "First reply",
                                      "url": "https://github.com/orgs/community/discussions/197089#discussioncomment-2",
                                      "createdAt": "2026-01-02T00:00:00Z",
                                      "author": { "login": "maintainer" }
                                    }
                                  }
                                ]
                              }
                            }
                          }
                        ],
                        "pageInfo": {
                          "hasNextPage": false,
                          "endCursor": null
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);
        var service = new GitHubService("test-token", new HttpClient(handler));

        var discussion = await service.GetOrganizationDiscussionWithCommentsAsync("community", 197089);

        Assert.IsNotNull(discussion);
        Assert.AreEqual("All GitHub Copilot plans are now on usage-based billing [FAQ]", discussion.Title);
        Assert.AreEqual("answer-1", discussion.AnswerId);
        Assert.AreEqual("https://github.com/orgs/community/discussions/197089", discussion.Url);
        Assert.HasCount(2, discussion.Comments);
        Assert.AreEqual("comment-1", discussion.Comments[0].Id);
        Assert.AreEqual("reply-1", discussion.Comments[1].Id);
        Assert.AreEqual("comment-1", discussion.Comments[1].ParentId);
        StringAssert.Contains(handler.RequestContent, "197089 org:community");
    }

    private sealed class CapturingGitHubMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public CapturingGitHubMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        public string RequestContent { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent)
            };
        }
    }
}
