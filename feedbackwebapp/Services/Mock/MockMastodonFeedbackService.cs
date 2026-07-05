using System.Threading.Tasks;
using FeedbackWebApp.Services.Authentication;
using Microsoft.Extensions.Configuration;
using SharedDump.Models;
using System.Collections.Generic;

namespace FeedbackWebApp.Services.Mock;

public class MockMastodonFeedbackService : IMastodonFeedbackService
{
    public MockMastodonFeedbackService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAuthenticationHeaderService authHeaderService)
    {
    }

    public Task<List<CommentThread>> SearchAsync(string query, string instance)
    {
        return Task.FromResult(new List<CommentThread>
        {
            new CommentThread
            {
                Id = "mock-123",
                Platform = "Mastodon",
                Title = "Mock Mastodon Post",
                Url = $"https://{instance}/@mockuser/123",
                Comments = new List<CommentData>
                {
                    new CommentData
                    {
                        Id = "mock-123",
                        Author = "MockUser",
                        Content = "This is a mock Mastodon post.",
                        CreatedAt = System.DateTime.UtcNow.AddHours(-2),
                        Metadata = new Dictionary<string, object?>
                        {
                            ["username"] = "mockuser",
                            ["avatar"] = $"https://{instance}/avatars/mock.png"
                        }
                    }
                },
                Metadata = new Dictionary<string, object?>
                {
                    ["instance"] = instance,
                    ["author"] = "mockuser",
                    ["created_at"] = System.DateTime.UtcNow.AddHours(-2)
                }
            }
        });
    }

    public Task<CommentThread?> GetThreadAsync(string statusUrl, string instance)
    {
        return Task.FromResult<CommentThread?>(new CommentThread
        {
            Id = "mock-123",
            Platform = "Mastodon",
            Title = "Mock Mastodon Post",
            Url = statusUrl,
            Comments = new List<CommentData>
            {
                new CommentData
                {
                    Id = "mock-123",
                    Author = "MockUser",
                    Content = "This is a mock Mastodon post.",
                    CreatedAt = System.DateTime.UtcNow.AddHours(-2),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["username"] = "mockuser",
                        ["avatar"] = $"https://{instance}/avatars/mock.png"
                    }
                },
                new CommentData
                {
                    Id = "mock-456",
                    Author = "ReplyUser",
                    Content = "This is a mock reply.",
                    CreatedAt = System.DateTime.UtcNow.AddMinutes(-90),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["username"] = "replyuser",
                        ["avatar"] = $"https://{instance}/avatars/reply.png"
                    }
                }
            },
            Metadata = new Dictionary<string, object?>
            {
                ["instance"] = instance,
                ["author"] = "mockuser",
                ["created_at"] = System.DateTime.UtcNow.AddHours(-2)
            }
        });
    }
}
