// filepath: c:\GitHub\feedbackflow\feedbackwebapp\Services\Mock\MockManualFeedbackService.cs
using FeedbackWebApp.Services.Feedback;
using FeedbackWebApp.Services.Interfaces;

namespace FeedbackWebApp.Services.Mock;

public class MockManualFeedbackService : FeedbackService, IManualFeedbackService
{
    public string CustomPrompt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public MockManualFeedbackService(
        IHttpClientFactory http,
        IConfiguration configuration,
        UserSettingsService userSettings,
        FeedbackStatusUpdate? onStatusUpdate = null)
        : base(http, configuration, userSettings, onStatusUpdate) { }

    public override async Task<(string markdownResult, object? additionalData)> GetFeedback()
    {
        await Task.Delay(500); // Simulate network delay
        
        UpdateStatus(FeedbackProcessStatus.GatheringComments, "Processing manual input...");
        await Task.Delay(1000); // Simulate analysis time
        
        var mockMarkdown = @"## Manual Input Analysis

### Overview
Analysis of manually provided content
Content length: " + Content.Length + @" characters

### Key Points
- 📝 Analysis of custom content
- 🔍 Identified main themes and insights
- 💡 Extracted actionable recommendations

### Detailed Breakdown

#### Main Themes
1. Content Overview
   - The provided content was successfully processed
   - " + (Content.Length > 100 ? "Substantial amount of content analyzed" : "Brief content provided for analysis") + @"

2. Sentiment Analysis
   - Overall sentiment appears balanced
   - Key emotional markers identified in the content

#### Recommendations
- Consider expanding on specific areas mentioned in the content
- Follow up on identified questions or concerns
- Use these insights to inform future decisions

### Conclusion
The manual content analysis provides valuable insights that can guide your understanding and decision-making process.";

        UpdateStatus(FeedbackProcessStatus.Completed, "Analysis completed");
        return (mockMarkdown, null);
    }
}
