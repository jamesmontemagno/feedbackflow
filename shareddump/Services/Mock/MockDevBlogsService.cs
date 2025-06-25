using SharedDump.Models.DevBlogs;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services.Mock;

public class MockDevBlogsService : IDevBlogsService
{
    private readonly List<DevBlogsArticle> _mockArticles = new()
    {
        new DevBlogsArticle
        {
            Title = "Announcing .NET 9: Performance Improvements and New Features",
            Summary = "Explore the latest performance enhancements, new APIs, and developer productivity improvements in .NET 9, including native AOT improvements and enhanced cloud-native features.",
            Link = "https://devblogs.microsoft.com/dotnet/announcing-dotnet-9/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-2),
            Authors = new[] { "Microsoft .NET Team" },
            Categories = new[] { ".NET", "Performance", "Cloud" },
            Guid = "dotnet-9-announcement-2024"
        },
        new DevBlogsArticle
        {
            Title = "Building Scalable Microservices with Azure Functions and .NET",
            Summary = "Learn how to design and implement scalable microservices using Azure Functions, including best practices for dependency injection, logging, and monitoring.",
            Link = "https://devblogs.microsoft.com/azure/microservices-azure-functions-dotnet/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-5),
            Authors = new[] { "Azure Functions Team", "Sarah Chen" },
            Categories = new[] { "Azure", "Microservices", ".NET", "Serverless" },
            Guid = "azure-functions-microservices-2024"
        },
        new DevBlogsArticle
        {
            Title = "Visual Studio 2024: AI-Powered Development Experience",
            Summary = "Discover how Visual Studio 2024 integrates AI assistance throughout the development workflow, from intelligent code completion to automated testing suggestions.",
            Link = "https://devblogs.microsoft.com/visualstudio/vs2024-ai-development/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-7),
            Authors = new[] { "Visual Studio Team", "Mark Johnson" },
            Categories = new[] { "Visual Studio", "AI", "Developer Tools", "Productivity" },
            Guid = "visual-studio-2024-ai-features"
        },
        new DevBlogsArticle
        {
            Title = "Blazor WebAssembly: Optimizing Performance for Large Applications",
            Summary = "Explore advanced techniques for optimizing Blazor WebAssembly applications, including lazy loading, prerendering strategies, and memory management best practices.",
            Link = "https://devblogs.microsoft.com/aspnet/blazor-wasm-performance-optimization/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-10),
            Authors = new[] { "ASP.NET Team", "Steve Sanderson" },
            Categories = new[] { "Blazor", "WebAssembly", "Performance", "ASP.NET" },
            Guid = "blazor-wasm-performance-2024"
        },
        new DevBlogsArticle
        {
            Title = "C# 13 Preview: New Language Features and Syntax Improvements",
            Summary = "Get an early look at C# 13's new features including enhanced pattern matching, improved nullable reference types, and new collection expressions.",
            Link = "https://devblogs.microsoft.com/dotnet/csharp-13-preview-features/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-14),
            Authors = new[] { "C# Language Team", "Mads Torgersen" },
            Categories = new[] { "C#", "Language Features", ".NET", "Programming" },
            Guid = "csharp-13-preview-2024"
        },
        new DevBlogsArticle
        {
            Title = "Azure Developer CLI: Streamlining Cloud Development Workflows",
            Summary = "Learn how Azure Developer CLI simplifies the developer experience for building, deploying, and monitoring cloud applications with integrated DevOps practices.",
            Link = "https://devblogs.microsoft.com/azure/azure-dev-cli-workflows/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-18),
            Authors = new[] { "Azure Developer Experience Team" },
            Categories = new[] { "Azure", "CLI", "DevOps", "Developer Tools" },
            Guid = "azure-dev-cli-workflows-2024"
        },
        new DevBlogsArticle
        {
            Title = "GitHub Copilot Integration in Visual Studio: Boosting Developer Productivity",
            Summary = "Explore how GitHub Copilot's integration with Visual Studio transforms the coding experience with context-aware suggestions and intelligent code generation.",
            Link = "https://devblogs.microsoft.com/visualstudio/github-copilot-integration-productivity/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-21),
            Authors = new[] { "GitHub Team", "Visual Studio Team" },
            Categories = new[] { "GitHub", "AI", "Visual Studio", "Productivity", "Copilot" },
            Guid = "github-copilot-vs-integration-2024"
        },
        new DevBlogsArticle
        {
            Title = "Entity Framework Core 9: Advanced Query Capabilities and Performance",
            Summary = "Discover the new query features in EF Core 9, including complex type support, improved LINQ translations, and significant performance optimizations.",
            Link = "https://devblogs.microsoft.com/dotnet/ef-core-9-advanced-queries/",
            PublishDate = DateTimeOffset.UtcNow.AddDays(-25),
            Authors = new[] { "Entity Framework Team", "Arthur Vickers" },
            Categories = new[] { "Entity Framework", ".NET", "Database", "Performance" },
            Guid = "ef-core-9-advanced-queries-2024"
        }
    };

    // Original service method for compatibility
    public async Task<DevBlogsArticleModel?> FetchArticleWithCommentsAsync(string articleUrl)
    {
        await Task.Delay(300); // Simulate API delay
        
        // For mock purposes, return a basic article model
        // In reality, this would parse the article URL and fetch comments
        var article = _mockArticles.FirstOrDefault(a => a.Link == articleUrl);
        if (article == null) return null;

        return new DevBlogsArticleModel
        {
            Title = article.Title,
            Url = article.Link,
            Comments = new List<DevBlogsCommentModel>()
        };
    }

    // New unified interface methods
    public async Task<List<DevBlogsArticle>> GetLatestArticlesAsync(int count = 10)
    {
        await Task.Delay(200); // Simulate API delay
        
        return _mockArticles
            .OrderByDescending(a => a.PublishDate)
            .Take(count)
            .ToList();
    }

    public async Task<List<DevBlogsArticle>> GetArticlesByCategoryAsync(string category, int count = 10)
    {
        await Task.Delay(150); // Simulate API delay
        
        return _mockArticles
            .Where(a => a.Categories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.PublishDate)
            .Take(count)
            .ToList();
    }

    public async Task<List<DevBlogsArticle>> SearchArticlesAsync(string query, int count = 10)
    {
        await Task.Delay(250); // Simulate API delay
        
        return _mockArticles
            .Where(a => 
                a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Categories.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.PublishDate)
            .Take(count)
            .ToList();
    }

    public async Task<DevBlogsArticle?> GetArticleByGuidAsync(string guid)
    {
        await Task.Delay(100); // Simulate API delay
        
        return _mockArticles.FirstOrDefault(a => a.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<string>> GetAvailableCategoriesAsync()
    {
        await Task.Delay(100); // Simulate API delay
        
        return _mockArticles
            .SelectMany(a => a.Categories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    public async Task<List<DevBlogsArticle>> GetArticlesByAuthorAsync(string author, int count = 10)
    {
        await Task.Delay(150); // Simulate API delay
        
        return _mockArticles
            .Where(a => a.Authors.Any(au => au.Contains(author, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(a => a.PublishDate)
            .Take(count)
            .ToList();
    }

    public async Task<List<DevBlogsArticle>> GetArticlesByDateRangeAsync(DateTimeOffset startDate, DateTimeOffset endDate, int count = 10)
    {
        await Task.Delay(200); // Simulate API delay
        
        return _mockArticles
            .Where(a => a.PublishDate >= startDate && a.PublishDate <= endDate)
            .OrderByDescending(a => a.PublishDate)
            .Take(count)
            .ToList();
    }
}
