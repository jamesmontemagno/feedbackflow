using SharedDump.Models.HackerNews;
using SharedDump.Models.YouTube;
using SharedDump.Models.Reddit;
using SharedDump.Models.GitHub;

namespace SharedDump.Services.Mock;

/// <summary>
/// Provides standardized mock data for different service types
/// </summary>
public static class MockDataProvider
{
    /// <summary>
    /// Gets mock HackerNews stories and comments
    /// </summary>
    public static class HackerNews
    {
        public static List<HackerNewsStory> GetMockStories()
        {
            return new List<HackerNewsStory>
            {
                new HackerNewsStory
                {
                    Id = 1,
                    Title = "Ask HN: What's your favorite programming language and why?",
                    Url = "https://news.ycombinator.com/item?id=1",
                    Score = 342,
                    By = "developer123",
                    Time = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
                    Descendants = 89,
                    Type = "story",
                    Kids = new[] { 2, 3, 4, 5, 6 }
                },
                new HackerNewsStory
                {
                    Id = 7,
                    Title = "Show HN: I built a tool to analyze GitHub repositories",
                    Url = "https://github.com/mockuser/repo-analyzer",
                    Score = 156,
                    By = "builderofthings",
                    Time = DateTimeOffset.UtcNow.AddHours(-4).ToUnixTimeSeconds(),
                    Descendants = 34,
                    Type = "story",
                    Kids = new[] { 8, 9, 10 }
                },
                new HackerNewsStory
                {
                    Id = 11,
                    Title = "The Future of Web Development in 2025",
                    Url = "https://techblog.example.com/future-web-dev-2025",
                    Score = 278,
                    By = "webguru",
                    Time = DateTimeOffset.UtcNow.AddHours(-6).ToUnixTimeSeconds(),
                    Descendants = 67,
                    Type = "story",
                    Kids = new[] { 12, 13, 14, 15 }
                }
            };
        }

        public static List<HackerNewsComment> GetMockComments()
        {
            return new List<HackerNewsComment>
            {
                new HackerNewsComment
                {
                    Id = 2,
                    By = "techentusiast",
                    Parent = 1,
                    Text = "I love C# for its type safety and the excellent tooling ecosystem. The async/await pattern makes concurrent programming much more approachable.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-90).ToUnixTimeSeconds(),
                    Type = "comment",
                    Kids = new[] { 3 }
                },
                new HackerNewsComment
                {
                    Id = 3,
                    By = "functionalfan",
                    Parent = 2,
                    Text = "Have you tried F#? It combines the .NET ecosystem with functional programming paradigms beautifully.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-75).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 4,
                    By = "rustacean",
                    Parent = 1,
                    Text = "Rust has been my go-to lately. The memory safety guarantees without garbage collection are game-changing for systems programming.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-60).ToUnixTimeSeconds(),
                    Type = "comment",
                    Kids = new[] { 5 }
                },
                new HackerNewsComment
                {
                    Id = 5,
                    By = "performance_matters",
                    Parent = 4,
                    Text = "The learning curve is steep though. But once you get it, the confidence in your code's correctness is unmatched.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-45).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 6,
                    By = "python_lover",
                    Parent = 1,
                    Text = "Python's simplicity and readability make it perfect for rapid prototyping and data science work. The ecosystem is incredible.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 8,
                    By = "opensource_advocate",
                    Parent = 7,
                    Text = "This looks really useful! Have you considered adding support for analyzing dependency graphs? That would be a killer feature.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-120).ToUnixTimeSeconds(),
                    Type = "comment",
                    Kids = new[] { 9 }
                },
                new HackerNewsComment
                {
                    Id = 9,
                    By = "builderofthings",
                    Parent = 8,
                    Text = "Great suggestion! That's actually on my roadmap for v2.0. The current version focuses on code quality metrics, but dependency analysis would be a natural extension.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-100).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 10,
                    By = "security_first",
                    Parent = 7,
                    Text = "Nice work! Does it scan for common security vulnerabilities as well? That would make it even more valuable for teams.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-80).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 12,
                    By = "frontend_dev",
                    Parent = 11,
                    Text = "The trend towards server-side rendering is interesting. WebAssembly is also opening up new possibilities.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-150).ToUnixTimeSeconds(),
                    Type = "comment",
                    Kids = new[] { 13 }
                },
                new HackerNewsComment
                {
                    Id = 13,
                    By = "wasm_enthusiast",
                    Parent = 12,
                    Text = "Absolutely! Being able to run C# and Rust in the browser natively is a game changer for certain applications.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-140).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 14,
                    By = "performance_guru",
                    Parent = 11,
                    Text = "I'm still skeptical about some of these frameworks. Sometimes simple HTML and CSS with minimal JavaScript is the way to go.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-130).ToUnixTimeSeconds(),
                    Type = "comment"
                },
                new HackerNewsComment
                {
                    Id = 15,
                    By = "webguru",
                    Parent = 14,
                    Text = "You're right that complexity can be an issue. The key is choosing the right tool for the job and not over-engineering.",
                    Time = DateTimeOffset.UtcNow.AddMinutes(-120).ToUnixTimeSeconds(),
                    Type = "comment"
                }
            };
        }

        /// <summary>
        /// Gets mock HackerNewsItems (legacy format) for compatibility
        /// </summary>
        public static List<List<HackerNewsItem>> GetMockItemThreads()
        {
            return new List<List<HackerNewsItem>>
            {
                new()
                {
                    new()
                    {
                        Id = 1,
                        Title = "Ask HN: What's your favorite programming language and why?",
                        By = "developer123",
                        Text = null,
                        Time = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
                        Kids = new List<int> { 2, 3, 4, 5, 6 }
                    },
                    new()
                    {
                        Id = 2,
                        By = "techentusiast",
                        Text = "I love C# for its type safety and the excellent tooling ecosystem.",
                        Time = DateTimeOffset.UtcNow.AddMinutes(-90).ToUnixTimeSeconds(),
                        Parent = 1
                    }
                },
                new()
                {
                    new()
                    {
                        Id = 7,
                        Title = "Show HN: I built a tool to analyze GitHub repositories",
                        By = "builderofthings",
                        Text = null,
                        Time = DateTimeOffset.UtcNow.AddHours(-4).ToUnixTimeSeconds(),
                        Kids = new List<int> { 8, 9, 10 }
                    },
                    new()
                    {
                        Id = 8,
                        By = "opensource_advocate",
                        Text = "This looks really useful! Great work on the dependency analysis feature.",
                        Time = DateTimeOffset.UtcNow.AddMinutes(-120).ToUnixTimeSeconds(),
                        Parent = 7
                    }
                }
            };
        }

        /// <summary>
        /// Gets formatted comment string for mock feedback analysis
        /// </summary>
        public static string GetFormattedComments()
        {
            return @"Comment by techentusiast: I love C# for its type safety and the excellent tooling ecosystem. The async/await pattern makes concurrent programming much more approachable.

Comment by functionalfan: Have you tried F#? It combines the .NET ecosystem with functional programming paradigms beautifully.

Comment by rustacean: Rust has been my go-to lately. The memory safety guarantees without garbage collection are game-changing for systems programming.

Comment by performance_matters: The learning curve is steep though. But once you get it, the confidence in your code's correctness is unmatched.

Comment by python_lover: Python's simplicity and readability make it perfect for rapid prototyping and data science work. The ecosystem is incredible.

Comment by opensource_advocate: This looks really useful! Have you considered adding support for analyzing dependency graphs? That would be a killer feature.

Comment by builderofthings: Great suggestion! That's actually on my roadmap for v2.0. The current version focuses on code quality metrics, but dependency analysis would be a natural extension.

Comment by security_first: Nice work! Does it scan for common security vulnerabilities as well? That would make it even more valuable for teams.

Comment by frontend_dev: The trend towards server-side rendering is interesting. WebAssembly is also opening up new possibilities.

Comment by wasm_enthusiast: Absolutely! Being able to run C# and Rust in the browser natively is a game changer for certain applications.

Comment by performance_guru: I'm still skeptical about some of these frameworks. Sometimes simple HTML and CSS with minimal JavaScript is the way to go.

Comment by webguru: You're right that complexity can be an issue. The key is choosing the right tool for the job and not over-engineering.";
        }
    }

    /// <summary>
    /// Gets mock YouTube data for testing
    /// </summary>
    public static class YouTube
    {
        public static List<YouTubeOutputVideo> GetMockVideos()
        {
            return new List<YouTubeOutputVideo>
            {
                new()
                {
                    Id = "abc123",
                    Title = "Getting Started with FeedbackFlow - Complete Tutorial",
                    Comments = new List<YouTubeOutputComment>
                    {
                        new()
                        { 
                            Id = "comment1",
                            Author = "DevEnthusiast",
                            Text = "Great tutorial! Very comprehensive and easy to follow. The step-by-step approach really helped me understand the concepts.",
                            PublishedAt = DateTime.UtcNow.AddDays(-1)
                        },
                        new()
                        { 
                            Id = "comment2",
                            Author = "CodeNewbie",
                            Text = "Could you make a follow-up video on advanced features? This was perfect for beginners.",
                            PublishedAt = DateTime.UtcNow.AddDays(-2)
                        },
                        new()
                        { 
                            Id = "comment3",
                            Author = "TechReviewer",
                            Text = "The integration examples at 8:45 were exactly what I needed. Saved me hours of research!",
                            PublishedAt = DateTime.UtcNow.AddDays(-3)
                        }
                    }
                },
                new()
                {
                    Id = "xyz789",
                    Title = "FeedbackFlow vs Traditional Analytics - Performance Comparison",
                    Comments = new List<YouTubeOutputComment>
                    {
                        new()
                        { 
                            Id = "comment4",
                            Author = "DataAnalyst",
                            Text = "Excellent explanation of the performance benefits. The benchmarks were very convincing.",
                            PublishedAt = DateTime.UtcNow.AddDays(-4)
                        },
                        new()
                        { 
                            Id = "comment5",
                            Author = "ProductManager",
                            Text = "This comparison helped me make the case to my team. We're implementing FeedbackFlow next quarter.",
                            PublishedAt = DateTime.UtcNow.AddDays(-5)
                        },
                        new()
                        { 
                            Id = "comment6",
                            Author = "StartupFounder",
                            Text = "Love the real-world examples. Can you share more about scaling for larger datasets?",
                            PublishedAt = DateTime.UtcNow.AddDays(-6)
                        }
                    }
                }
            };
        }

        public static string GetFormattedComments()
        {
            var videos = GetMockVideos();
            return string.Join("\n\n", videos.SelectMany(v => 
                v.Comments.Select(c => $"Video: {v.Title}\nComment by {c.Author}: {c.Text}")));
        }
    }

    // We can add other platforms here in the future:
    // public static class Reddit { ... }
    // public static class GitHub { ... }
}
