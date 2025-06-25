using SharedDump.Models.YouTube;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of IYouTubeService for testing and development.
/// Provides realistic YouTube data without requiring API access.
/// </summary>
public class MockYouTubeService : IYouTubeService
{
    public async Task<IEnumerable<string>> GetPlaylistVideos(string playlistId)
    {
        // Simulate network delay
        await Task.Delay(300);

        // Return mock video IDs for playlist
        return new List<string>
        {
            "mockVideoId1",
            "mockVideoId2", 
            "mockVideoId3",
            "mockVideoId4",
            "mockVideoId5"
        };
    }

    public async Task<IEnumerable<string>> GetChannelVideos(string channelId)
    {
        // Simulate network delay
        await Task.Delay(400);

        // Return mock video IDs for channel
        return new List<string>
        {
            "mockChannelVideo1",
            "mockChannelVideo2",
            "mockChannelVideo3",
            "mockChannelVideo4"
        };
    }

    public async Task<YouTubeOutputVideo> ProcessVideo(string videoId)
    {
        // Simulate processing delay
        await Task.Delay(800);

        // Generate mock video data based on the video ID
        var baseDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30));
        
        return new YouTubeOutputVideo
        {
            Id = videoId,
            Title = $"Mock Video: {GetMockTitle(videoId)}",
            Url = $"https://www.youtube.com/watch?v={videoId}",
            UploadDate = baseDate,
            PublishedAt = baseDate,
            ChannelId = "mockChannelId123",
            ChannelTitle = "Mock Developer Channel",
            Description = GetMockDescription(videoId),
            ViewCount = Random.Shared.Next(1000, 100000),
            LikeCount = Random.Shared.Next(50, 5000),
            CommentCount = Random.Shared.Next(10, 500),
            Comments = GenerateMockComments(videoId, baseDate)
        };
    }

    public async Task<List<YouTubeOutputVideo>> SearchVideos(string topic, string tag, DateTimeOffset cutoffDate)
    {
        // Simulate search delay
        await Task.Delay(1000);

        var videos = new List<YouTubeOutputVideo>();
        var videoCount = Random.Shared.Next(3, 8);

        for (int i = 0; i < videoCount; i++)
        {
            var videoId = $"searchVideo_{topic}_{i}";
            var publishDate = cutoffDate.AddDays(Random.Shared.Next(1, 30));
            
            videos.Add(new YouTubeOutputVideo
            {
                Id = videoId,
                Title = $"Mock Search Result: {topic} - Video {i + 1}",
                Url = $"https://www.youtube.com/watch?v={videoId}",
                UploadDate = publishDate.DateTime,
                PublishedAt = publishDate,
                ChannelId = $"mockSearchChannel{i}",
                ChannelTitle = $"Mock Channel {i + 1}",
                Description = $"This is a mock video about {topic}. Tagged with {tag}.",
                ViewCount = Random.Shared.Next(5000, 50000),
                LikeCount = Random.Shared.Next(100, 2000),
                CommentCount = Random.Shared.Next(20, 300),
                Comments = GenerateMockComments(videoId, publishDate.DateTime)
            });
        }

        return videos;
    }

    public async Task<List<YouTubeOutputVideo>> SearchVideosBasicInfo(string searchQuery, string tag, DateTimeOffset publishedAfter)
    {
        // Simulate search delay
        await Task.Delay(600);

        var videos = new List<YouTubeOutputVideo>();
        var videoCount = Random.Shared.Next(5, 12);

        for (int i = 0; i < videoCount; i++)
        {
            var videoId = $"basicSearchVideo_{searchQuery.Replace(" ", "_")}_{i}";
            var publishDate = publishedAfter.AddDays(Random.Shared.Next(1, 20));
            
            videos.Add(new YouTubeOutputVideo
            {
                Id = videoId,
                Title = $"Mock Basic Result: {searchQuery} Tutorial {i + 1}",
                Url = $"https://www.youtube.com/watch?v={videoId}",
                UploadDate = publishDate.DateTime,
                PublishedAt = publishDate,
                ChannelId = $"mockBasicChannel{i}",
                ChannelTitle = $"Tech Tutorial Channel {i + 1}",
                Description = $"Basic info mock video for search: {searchQuery}",
                ViewCount = Random.Shared.Next(1000, 25000),
                LikeCount = Random.Shared.Next(50, 1000),
                CommentCount = Random.Shared.Next(5, 150),
                Comments = [] // Basic info doesn't include comments
            });
        }

        return videos;
    }

    private static string GetMockTitle(string videoId)
    {
        var titles = new[]
        {
            "Building Better Apps with Modern Frameworks",
            "Understanding Design Patterns in Software Development", 
            "Performance Optimization Tips and Tricks",
            "Getting Started with Cloud Development",
            "API Best Practices for Developers",
            "Database Design Fundamentals",
            "Security Considerations for Web Applications",
            "Debugging Techniques That Actually Work"
        };

        var hash = videoId.GetHashCode();
        var index = Math.Abs(hash) % titles.Length;
        return titles[index];
    }

    private static string GetMockDescription(string videoId)
    {
        var descriptions = new[]
        {
            "In this comprehensive tutorial, we dive deep into modern development practices and explore the latest tools and techniques that can help you build better applications.",
            "Join us as we explore fundamental concepts that every developer should know. This video covers essential patterns and provides practical examples you can use immediately.",
            "Performance is crucial for user experience. Learn about profiling, optimization strategies, and common pitfalls to avoid in your applications.",
            "Cloud development has transformed how we build and deploy applications. Discover the benefits and learn how to get started with cloud-native development.",
            "APIs are the backbone of modern applications. This video covers design principles, versioning strategies, and security considerations for robust API development."
        };

        var hash = videoId.GetHashCode();
        var index = Math.Abs(hash) % descriptions.Length;
        return descriptions[index];
    }

    private static List<YouTubeOutputComment> GenerateMockComments(string videoId, DateTime baseDate)
    {
        var comments = new List<YouTubeOutputComment>();
        var commentCount = Random.Shared.Next(5, 20);

        var sampleComments = new[]
        {
            "Great tutorial! This really helped me understand the concepts better.",
            "Thanks for the clear explanation. Could you do a follow-up video on advanced topics?",
            "I've been struggling with this for weeks. Your explanation finally made it click!",
            "Excellent content as always. Keep up the great work!",
            "This is exactly what I needed for my current project. Thank you!",
            "Very helpful video. The examples are really well chosen.",
            "Love the step-by-step approach. Makes it easy to follow along.",
            "Can you share the source code for this example?",
            "This solved a problem I've been having at work. Much appreciated!",
            "Clear, concise, and practical. Exactly what YouTube needs more of."
        };

        var authors = new[]
        {
            "DevLearner123", "CodeNewbie", "TechEnthusiast", "ProgrammerPro", "WebDevWiz",
            "AppBuilder", "DataDriven", "CloudExplorer", "APIExpert", "DebugMaster"
        };

        for (int i = 0; i < commentCount; i++)
        {
            var commentDate = baseDate.AddHours(Random.Shared.Next(1, 72));
            var commentIndex = Random.Shared.Next(sampleComments.Length);
            var authorIndex = Random.Shared.Next(authors.Length);

            comments.Add(new YouTubeOutputComment
            {
                Id = $"mockComment_{videoId}_{i}",
                Author = authors[authorIndex],
                Text = sampleComments[commentIndex],
                PublishedAt = commentDate,
                ParentId = null // For simplicity, no nested comments in mock data
            });
        }

        // Sort comments by publish date (newest first)
        return comments.OrderByDescending(c => c.PublishedAt).ToList();
    }
}
