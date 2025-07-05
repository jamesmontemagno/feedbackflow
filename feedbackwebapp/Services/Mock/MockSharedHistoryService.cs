using FeedbackWebApp.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SharedDump.Models;

namespace FeedbackWebApp.Services.Mock;

/// <summary>
/// Mock implementation of ISharedHistoryService for testing and development
/// </summary>
public class MockSharedHistoryService : ISharedHistoryService
{
    private readonly ILogger<MockSharedHistoryService> _logger;
    private readonly List<SharedAnalysisEntity> _mockSavedAnalyses;

    public MockSharedHistoryService(ILogger<MockSharedHistoryService> logger)
    {
        _logger = logger;
        _mockSavedAnalyses = GenerateMockSavedAnalyses();
    }

    public async Task<List<SharedAnalysisEntity>> GetUsersSavedAnalysesAsync()
    {
        _logger.LogInformation("Mock: Getting user's saved analyses");
        await Task.Delay(500); // Simulate network delay
        return new List<SharedAnalysisEntity>(_mockSavedAnalyses);
    }

    public async Task<bool> DeleteSharedAnalysisAsync(string id)
    {
        _logger.LogInformation("Mock: Deleting shared analysis {Id}", id);
        await Task.Delay(300); // Simulate network delay
        
        var analysisToRemove = _mockSavedAnalyses.FirstOrDefault(a => a.Id == id);
        if (analysisToRemove != null)
        {
            _mockSavedAnalyses.Remove(analysisToRemove);
            _logger.LogInformation("Mock: Successfully deleted analysis {Id}", id);
            return true;
        }

        _logger.LogWarning("Mock: Analysis {Id} not found for deletion", id);
        return false;
    }

    public async Task<AnalysisData?> GetSharedAnalysisDataAsync(string id)
    {
        _logger.LogInformation("Mock: Getting full analysis data for {Id}", id);
        await Task.Delay(400); // Simulate network delay

        var analysis = _mockSavedAnalyses.FirstOrDefault(a => a.Id == id);
        if (analysis == null)
        {
            return null;
        }

        // Return a full AnalysisData object based on the saved analysis metadata
        return new AnalysisData
        {
            Id = analysis.Id,
            Title = analysis.Title,
            Summary = analysis.Summary,
            FullAnalysis = GenerateFullAnalysisContent(analysis),
            SourceType = analysis.SourceType,
            UserInput = analysis.UserInput,
            CreatedDate = analysis.CreatedDate
        };
    }

    public async Task<int> GetSavedAnalysesCountAsync()
    {
        _logger.LogInformation("Mock: Getting saved analyses count");
        await Task.Delay(100);
        return _mockSavedAnalyses.Count;
    }

    public async Task<List<SharedAnalysisEntity>> SearchUsersSavedAnalysesAsync(string searchTerm)
    {
        _logger.LogInformation("Mock: Searching saved analyses with term '{SearchTerm}'", searchTerm);
        await Task.Delay(300);

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<SharedAnalysisEntity>(_mockSavedAnalyses);
        }

        var searchTermLower = searchTerm.ToLowerInvariant();
        return _mockSavedAnalyses.Where(analysis =>
            analysis.Title.ToLowerInvariant().Contains(searchTermLower) ||
            analysis.Summary.ToLowerInvariant().Contains(searchTermLower) ||
            analysis.SourceType.ToLowerInvariant().Contains(searchTermLower) ||
            (!string.IsNullOrEmpty(analysis.UserInput) && analysis.UserInput.ToLowerInvariant().Contains(searchTermLower))
        ).ToList();
    }

    public async Task<string> ShareAnalysisAsync(AnalysisData analysis, bool isPublic = false)
    {
        _logger.LogInformation("Mock: Sharing analysis '{Title}' (Public: {IsPublic})", analysis.Title, isPublic);
        await Task.Delay(800); // Simulate network delay for sharing

        // Generate a mock shared ID
        var sharedId = Guid.NewGuid().ToString();
        
        // Add to mock saved analyses
        var newAnalysis = new SharedAnalysisEntity("mock-user-123", sharedId, analysis, isPublic);
        _mockSavedAnalyses.Add(newAnalysis);
        
        _logger.LogInformation("Mock: Successfully shared analysis with ID {SharedId}", sharedId);
        return sharedId;
    }

    public async Task<bool> UpdateAnalysisVisibilityAsync(string analysisId, bool isPublic)
    {
        _logger.LogInformation("Mock: Updating analysis {Id} visibility to {IsPublic}", analysisId, isPublic);
        await Task.Delay(300);

        var analysis = _mockSavedAnalyses.FirstOrDefault(a => a.Id == analysisId);
        if (analysis != null)
        {
            analysis.IsPublic = isPublic;
            analysis.PublicSharedDate = isPublic ? DateTime.UtcNow : null;
            _logger.LogInformation("Mock: Successfully updated analysis {Id} visibility", analysisId);
            return true;
        }

        _logger.LogWarning("Mock: Analysis {Id} not found for visibility update", analysisId);
        return false;
    }

    public async Task<string?> GetPublicShareLinkAsync(string analysisId)
    {
        _logger.LogInformation("Mock: Getting public share link for analysis {Id}", analysisId);
        await Task.Delay(100);

        var analysis = _mockSavedAnalyses.FirstOrDefault(a => a.Id == analysisId);
        if (analysis != null && analysis.IsPublic)
        {
            return $"https://feedbackflow.example.com/shared/{analysisId}";
        }

        return null;
    }

    public async Task<AnalysisData?> GetSharedAnalysisAsync(string id)
    {
        // This is the same as GetSharedAnalysisDataAsync
        return await GetSharedAnalysisDataAsync(id);
    }

    public async Task<List<AnalysisHistoryItem>> GetSharedAnalysisHistoryAsync()
    {
        _logger.LogInformation("Mock: Getting shared analysis history");
        await Task.Delay(400);

        // Convert saved analyses to history items
        return _mockSavedAnalyses.Select(analysis => new AnalysisHistoryItem
        {
            Id = analysis.Id,
            FullAnalysis = GenerateFullAnalysisContent(analysis),
            SourceType = analysis.SourceType,
            UserInput = analysis.UserInput,
            Timestamp = analysis.CreatedDate,
            IsShared = true,
            SharedId = analysis.Id,
            SharedDate = analysis.CreatedDate
        }).ToList();
    }

    private static List<SharedAnalysisEntity> GenerateMockSavedAnalyses()
    {
        var userId = "mock-user-123";
        return new List<SharedAnalysisEntity>
        {
            new SharedAnalysisEntity(userId, "analysis-1", new AnalysisData
            {
                Title = "GitHub Repository Feedback Analysis",
                Summary = "Analysis of user feedback from Microsoft/dotnet repository showing positive sentiment around new C# 12 features with some concerns about breaking changes.",
                SourceType = "GitHub",
                UserInput = "Analyze feedback from dotnet/core repository issues",
                CreatedDate = DateTime.UtcNow.AddDays(-5)
            }, isPublic: true), // This one is public
            
            new SharedAnalysisEntity(userId, "analysis-2", new AnalysisData
            {
                Title = "YouTube Video Comments Analysis",
                Summary = "Comprehensive analysis of comments from a technical tutorial video showing high engagement and positive learning outcomes.",
                SourceType = "YouTube",
                UserInput = "Please analyze comments from my latest .NET tutorial video",
                CreatedDate = DateTime.UtcNow.AddDays(-3)
            }, isPublic: false), // Private
            
            new SharedAnalysisEntity(userId, "analysis-3", new AnalysisData
            {
                Title = "Reddit Community Discussion Analysis",
                Summary = "Analysis of r/programming discussion about remote work trends showing mixed opinions with strong preference for hybrid models.",
                SourceType = "Reddit",
                UserInput = null,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            }, isPublic: true), // This one is public
            
            new SharedAnalysisEntity(userId, "analysis-4", new AnalysisData
            {
                Title = "Hacker News Startup Feedback",
                Summary = "Analysis of feedback from Show HN post about a new developer tool, highlighting feature requests and usability concerns.",
                SourceType = "HackerNews",
                UserInput = "Analyze the feedback from my Show HN post",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            }, isPublic: false), // Private
            
            new SharedAnalysisEntity(userId, "analysis-5", new AnalysisData
            {
                Title = "Manual Survey Analysis",
                Summary = "Analysis of manually entered survey responses about team productivity tools, showing strong preference for integration capabilities.",
                SourceType = "Manual",
                UserInput = "Analyze survey responses about productivity tools in our team",
                CreatedDate = DateTime.UtcNow.AddHours(-6)
            }, isPublic: false) // Private
        };
    }

    private static string GenerateFullAnalysisContent(SharedAnalysisEntity analysis)
    {
        return analysis.SourceType switch
        {
            "GitHub" => $@"# {analysis.Title}

## Overview
{analysis.Summary}

## Key Findings

### Sentiment Analysis
- **Positive**: 65% - Users appreciate the new features and performance improvements
- **Neutral**: 20% - Mixed reactions to implementation approaches
- **Negative**: 15% - Concerns about breaking changes and migration complexity

### Main Topics
1. **New Language Features**: High enthusiasm for pattern matching and records
2. **Performance**: Significant improvements noted in runtime performance
3. **Breaking Changes**: Some concern about upgrade path complexity
4. **Documentation**: Generally positive feedback on improved docs

### Recommendations
- Provide more migration guides for breaking changes
- Continue focus on performance improvements
- Expand documentation with more real-world examples

## Detailed Analysis
[This would contain the full detailed analysis in a real scenario]

*Analysis generated on {analysis.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC*",

            "YouTube" => $@"# {analysis.Title}

## Overview
{analysis.Summary}

## Engagement Metrics
- **Total Comments**: 156
- **Average Engagement**: High
- **Sentiment**: Mostly Positive

## Key Themes

### Learning Outcomes
- 78% of commenters found the content helpful
- Specific appreciation for step-by-step explanations
- Requests for follow-up tutorials on advanced topics

### Technical Feedback
- Code examples were clear and well-explained
- Suggestions for covering error handling
- Interest in performance optimization techniques

### Community Response
- Strong engagement with Q&A in comments
- Multiple requests for source code repository
- Positive feedback on teaching style

## Recommendations
- Create follow-up videos on advanced topics
- Provide downloadable source code
- Consider creating a series on related topics

*Analysis generated on {analysis.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC*",

            "Reddit" => $@"# {analysis.Title}

## Overview
{analysis.Summary}

## Discussion Analysis

### Sentiment Distribution
- **Positive**: 45% - Embracing remote/hybrid work benefits
- **Neutral**: 30% - Pragmatic views on situational needs
- **Negative**: 25% - Concerns about collaboration and culture

### Key Arguments

#### Pro-Remote
- Improved work-life balance
- Reduced commute time and costs
- Access to global talent pool
- Increased productivity for focused work

#### Pro-Office
- Better collaboration and mentorship
- Stronger team culture and relationships
- Easier knowledge sharing
- Clear work-life boundaries

#### Hybrid Preference
- Best of both worlds approach
- Flexibility based on task type
- Team meeting days in office
- Focus work from home

## Community Insights
The r/programming community shows a mature understanding of trade-offs, with most favoring flexible hybrid approaches rather than extremes.

*Analysis generated on {analysis.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC*",

            "HackerNews" => $@"# {analysis.Title}

## Overview
{analysis.Summary}

## Hacker News Community Response

### Reception
- **Score**: 156 points
- **Comments**: 34
- **Engagement**: High for Show HN post

### Feedback Categories

#### Feature Requests (40%)
- Integration with popular IDEs
- Support for additional languages
- API for custom analysis rules
- Export functionality for reports

#### Technical Questions (30%)
- Architecture and scalability approach
- Open source availability
- Performance benchmarks
- Security considerations

#### Usability Feedback (20%)
- User interface improvements
- Documentation requests
- Onboarding experience
- Mobile support

#### Business Model (10%)
- Pricing concerns
- Licensing questions
- Commercial vs open source features

### Notable Comments
- Several experienced developers expressed interest in contributing
- Questions about integration with existing CI/CD pipelines
- Positive feedback on problem statement and approach

## Action Items
Based on community feedback, prioritize IDE integration and consider open sourcing core functionality.

*Analysis generated on {analysis.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC*",

            _ => $@"# {analysis.Title}

## Overview
{analysis.Summary}

## Analysis Results

This is a comprehensive analysis of the provided feedback data. The analysis includes sentiment analysis, key themes identification, and actionable recommendations.

### Key Findings
- Detailed analysis of feedback patterns
- Identification of main themes and concerns
- Sentiment distribution across responses
- Actionable recommendations for improvement

### Methodology
The analysis used advanced natural language processing techniques to extract insights from the feedback data.

*Analysis generated on {analysis.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC*"
        };
    }
}
