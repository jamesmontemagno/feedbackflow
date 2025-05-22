using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Mock;

public class MockAutoDataSourceFeedbackService : FeedbackService, IAutoDataSourceFeedbackService
{
    public MockAutoDataSourceFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate)
    {
    }

    public override async Task<(string rawComments, int commentCount, object? additionalData)> GetComments()
    {
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Fetching comments from multiple sources...");
        await Task.Delay(1000); // Simulate network delay

        var mockComments = @"Source: YouTube
Video: Getting Started with FeedbackFlow
Comments:
- Great tutorial! Very helpful for setting up automation.
- The examples made it really clear how to integrate different platforms.

Source: GitHub
Issue #123: Feature Request - Add Slack Integration
Comments:
- +1 for Slack integration
- Would be really useful for our team workflow
- Could we also get Microsoft Teams support?

Source: Dev.to
Article: FeedbackFlow: Automating User Feedback Analysis
Comments:
- This looks promising! Just what we needed.
- How does it handle large volumes of feedback?
- Does it support custom data sources?";

        // Count total number of comments (lines starting with -)
        int commentCount = mockComments.Split('\n').Count(line => line.Trim().StartsWith('-'));

        return (mockComments, commentCount, null);
    }

    public override async Task<(string markdownResult, object? additionalData)> AnalyzeComments(string comments, int? commentCount = null, object? additionalData = null)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        UpdateStatus(FeedbackProcessStatus.AnalyzingComments, "Analyzing feedback from multiple sources...");
        await Task.Delay(1000); // Simulate analysis time

        // Use provided comment count or calculate
        int totalComments = commentCount ?? comments.Split('\n').Count(line => line.Trim().StartsWith('-'));

        var mockAnalysis = @$"# Multi-Source Feedback Analysis 📊

## Overview
Analysis of {totalComments} feedback items aggregated from multiple sources including YouTube, GitHub, and Dev.to.

## Key Themes 🎯

### Feature Requests
1. **Platform Integrations**
   - Slack integration highly requested
   - Microsoft Teams integration suggested
   - Custom data source support inquired

### User Experience
- Tutorial content well-received
- Clear examples appreciated
- Setup process deemed straightforward

### Technical Concerns
- Questions about scalability
- Interest in custom integration options

## Engagement Statistics 📈
- Total sources analyzed: 3
- Total comments processed: {totalComments}
- Average sentiment: Positive

## Recommendations 💡
1. Consider prioritizing Slack integration
2. Address scalability questions in documentation
3. Highlight custom integration capabilities
4. Plan follow-up content on advanced features

## Source Breakdown
- YouTube: Tutorial engagement positive
- GitHub: Integration requests dominant
- Dev.to: Technical queries prevalent";

        return (mockAnalysis, additionalData);
    }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        // Get comments
        var (comments, commentCount, additionalData) = await GetComments();
        
        if (string.IsNullOrWhiteSpace(comments))
        {
            return ("## No Comments Available\n\nThere are no comments to analyze at this time.", null);
        }

        // Analyze comments with count
        return await AnalyzeComments(comments, commentCount, additionalData);
    }
}
