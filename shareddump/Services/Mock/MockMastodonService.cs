using SharedDump.Models;
using Microsoft.Extensions.Logging;

namespace SharedDump.Services.Mock;

public class MockMastodonService : IMastodonService
{
    private readonly ILogger<MockMastodonService> _logger;

    public MockMastodonService(ILogger<MockMastodonService> logger)
    {
        _logger = logger;
    }

    public Task<List<CommentThread>> SearchAsync(string query, string instance, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Mock: Searching Mastodon for '{query}' on instance '{instance}'");
        return Task.FromResult(new List<CommentThread>
        {
            new CommentThread
            {
                Id = "123456",
                Platform = "Mastodon",
                Title = "Sample Mastodon Post",
                Url = $"https://{instance}/@sampleuser/123456",
                Comments = new List<CommentData>
                {
                    new CommentData
                    {
                        Id = "123456",
                        Author = "SampleUser",
                        Content = "This is a sample Mastodon post.",
                        CreatedAt = DateTime.UtcNow.AddHours(-1),
                        Metadata = new Dictionary<string, object?>
                        {
                            ["username"] = "sampleuser",
                            ["avatar"] = "https://{instance}/avatars/sample.png"
                        }
                    }
                },
                Metadata = new Dictionary<string, object?>
                {
                    ["instance"] = instance,
                    ["author"] = "sampleuser",
                    ["created_at"] = DateTime.UtcNow.AddHours(-1)
                }
            }
        });
    }

    public Task<CommentThread?> GetThreadAsync(string statusUrl, string instance, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Mock: Getting Mastodon thread for '{statusUrl}' on instance '{instance}'");
        return Task.FromResult<CommentThread?>(new CommentThread
        {
            Id = "123456",
            Platform = "Mastodon",
            Title = "Sample Mastodon Post",
            Url = statusUrl,
            Comments = new List<CommentData>
            {
                new CommentData
                {
                    Id = "123456",
                    Author = "SampleUser",
                    Content = "This is a sample Mastodon post.",
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["username"] = "sampleuser",
                        ["avatar"] = "https://{instance}/avatars/sample.png"
                    }
                },
                new CommentData
                {
                    Id = "654321",
                    Author = "ReplyUser",
                    Content = "This is a reply to the sample post.",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["username"] = "replyuser",
                        ["avatar"] = "https://{instance}/avatars/reply.png"
                    }
                }
            },
            Metadata = new Dictionary<string, object?>
            {
                ["instance"] = instance,
                ["author"] = "sampleuser",
                ["created_at"] = DateTime.UtcNow.AddHours(-1)
            }
        });
    }
}
