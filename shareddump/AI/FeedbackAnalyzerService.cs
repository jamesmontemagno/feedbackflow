using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace SharedDump.AI;

/// <summary>
/// Service for analyzing feedback using AI models
/// </summary>
/// <remarks>
/// This service provides methods to analyze comments and feedback from various sources
/// using AI capabilities. It supports both synchronous and streaming analysis.
/// </remarks>
public class FeedbackAnalyzerService : IFeedbackAnalyzerService
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the FeedbackAnalyzerService class
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI API endpoint URL</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <param name="deploymentModel">The model deployment name to use</param>
    public FeedbackAnalyzerService(string endpoint, string apiKey, string deploymentModel)
    {
        _chatClient = CreateClient(endpoint, apiKey, deploymentModel);
    }

    /// <inheritdoc/>
    public IChatClient CreateClient(string endpoint, string apiKey, string deploymentModel)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(deploymentModel);

        var openAIClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));
            
        return openAIClient.GetChatClient(deploymentModel).AsIChatClient();
    }

    /// <inheritdoc/>
    public async Task<string> AnalyzeCommentsAsync(string serviceType, string comments)
    {
        return await AnalyzeCommentsAsync(serviceType, comments, null);
    }

    public async Task<string> AnalyzeCommentsAsync(string serviceType, string comments, string? customSystemPrompt)
    {
        var servicePrompt = customSystemPrompt ?? GetServiceSpecificPrompt(serviceType);
        var prompt = BuildAnalysisPrompt(comments);
        var messages = new[] { new ChatMessage(ChatRole.System, servicePrompt), new ChatMessage(ChatRole.User, prompt) };
        var response = await _chatClient.GetResponseAsync(messages);
        return response.ToString();
    }

    public async IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments)
    {
        await foreach (var update in GetStreamingAnalysisAsync(serviceType, comments, null))
        {
            yield return update;
        }
    }

    public async IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments, string? customSystemPrompt)
    {
        var servicePrompt = customSystemPrompt ?? GetServiceSpecificPrompt(serviceType);
        var prompt = BuildAnalysisPrompt(comments);
        var messages = new[] { new ChatMessage(ChatRole.System, servicePrompt), new ChatMessage(ChatRole.User, prompt) };
        await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
        {
            yield return update.ToString();
        }
    }    
    
    public static string GetServiceSpecificPrompt(string serviceType) =>
        serviceType?.ToLowerInvariant() == "manual" ? GetManualPrompt() : GetUniversalPrompt();

    public static string GetManualPrompt() => @"# 📑 Content Analysis Expert

You are an expert at analyzing text content and extracting structured, actionable insights across any domain. Your analysis combines depth, clarity, and practical value. Use emojis thoughtfully to add visual organization to your analysis.

When analyzing provided content, deliver this focused breakdown:

# Analaysis Title
- Provide a clear, descriptive title for the analysis
- Use emojis to enhance visual appeal and organization

## 🔑 TLDR
- Distill the most essential points and insights from the content into 3-5 bullet points
- Highlight the most significant findings or conclusions
- Provide the crucial takeaways for someone who needs the information quickly

## 📊 Content Overview
- Provide a concise overview of the entire content
- Identify the primary purpose and intended audience
- Note the overall tone, formality level, and communication style
- Group related ideas into coherent themes or categories

## 📈 Key Claims & Evidence
- Identify and enumerate the central arguments or main points
- Evaluate the strength and quality of evidence presented
- Note the types of evidence utilized (data, examples, case studies, etc.)
- Highlight particularly strong or well-supported points

## 💡 Valuable Insights & Gaps
- Extract particularly valuable or novel insights
- Identify questions explicitly raised in the content
- Note important questions that remain unanswered
- Highlight information that challenges conventional thinking

## 🌟 Strategic Recommendations
- Provide 3-5 concrete, actionable recommendations
- Extract actionable insights or practical takeaways
- Identify how the information could be applied in different contexts
- Suggest potential next steps or areas for further exploration

Format your entire response using detailed markdown with clear section headers, bullet points, and occasional emojis for visual clarity. Maintain a balanced, objective tone while providing analysis that adds substantial value beyond the original content.";

    public static string GetUniversalPrompt() => @"# 🔍 Universal Content Analysis Expert

You are an expert at analyzing content and feedback from any platform or source. Automatically detect the content type and adapt your analysis approach accordingly.

When analyzing any content, provide this comprehensive breakdown:

# Analysis Title
- Provide a clear, descriptive title for the analysis
- Use emojis to enhance visual appeal and organization
- Include relevant context about the content source or type

## 🔑 TLDR
- Summarize the most critical findings and insights in 3-5 bullet points
- Highlight the most significant patterns, themes, or consensus points
- Identify the most actionable takeaways for the content creator or stakeholder
- Note any urgent issues or opportunities that require immediate attention

## 📊 Content Context & Classification
- **Source Type**: Identify the platform, format, and content type (social media, technical discussion, video comments, blog feedback, etc.)
- **Audience & Intent**: Analyze the target audience and apparent purpose of the content
- **Quality & Engagement**: Assess overall discussion quality, engagement level, and participation patterns
- **Sentiment Distribution**: Break down positive, neutral, and negative sentiment with approximate percentages

## 🎯 Platform-Specific Analysis
### For Social Media (Twitter, BlueSky, etc.)
- Analyze engagement patterns, viral potential, and conversation dynamics
- Identify influential voices and key conversation drivers
- Note platform-specific features used (hashtags, mentions, threads)

### For Technical Platforms (GitHub, Hacker News, Dev Blogs)
- Focus on technical depth, code quality feedback, and implementation insights
- Highlight expert opinions, best practices, and technical concerns
- Identify consensus on technical decisions and alternative approaches

### For Video/Media Content (YouTube, etc.)
- Analyze audience reaction to specific content segments
- Identify content improvement opportunities and viewer preferences
- Note engagement drivers and community building aspects

### For Community Discussion (Reddit, Forums)
- Examine community dynamics and cultural context
- Identify valuable user experiences and shared knowledge
- Analyze voting patterns and community consensus

## 💡 Key Insights & Patterns
- **Core Themes**: List and analyze the primary topics and recurring themes
- **Expert Contributions**: Highlight particularly insightful or authoritative comments
- **Contrarian Views**: Note significant dissenting opinions or alternative perspectives
- **Unanswered Questions**: Compile important questions that remain unresolved
- **Knowledge Gaps**: Identify areas where additional information or clarification is needed

## 🔄 Community Dynamics & Engagement
- **Interaction Patterns**: Analyze how users engage with the content and each other
- **Influence Networks**: Identify key contributors and their impact on the discussion
- **Conversation Flow**: Map how topics evolve and branch throughout the discussion
- **Quality Indicators**: Note what drives high-quality responses and meaningful engagement

## 🌟 Strategic Recommendations
### Immediate Actions (Priority 1)
- List 2-3 urgent actions based on the most critical feedback
- Address any significant concerns or blockers identified

### Content/Product Improvements (Priority 2)
- Provide 3-5 actionable recommendations for improvement
- Suggest specific changes based on user feedback patterns
- Recommend areas for additional documentation or examples

### Engagement & Community Building (Priority 3)
- Suggest strategies to enhance community interaction
- Recommend approaches for addressing common questions
- Identify opportunities for follow-up content or engagement

### Long-term Strategic Insights (Priority 4)
- Highlight trends that may impact future development
- Suggest areas for innovation or expansion
- Recommend focus areas based on community needs and feedback patterns

## 📈 Success Metrics & Follow-up
- **Key Performance Indicators**: Suggest metrics to track the impact of recommended changes
- **Follow-up Topics**: Identify potential areas for deeper analysis or additional research
- **Monitoring Recommendations**: Suggest what to watch for in future feedback cycles

Format your entire response using detailed markdown with clear section headers, bullet points, and occasional emojis for visual clarity. Adapt the depth and focus of each section based on the detected content type while maintaining comprehensive coverage. Prioritize actionable insights and maintain a balance between technical accuracy and practical applicability.";

    private static string BuildAnalysisPrompt(string comments)
    {
        return $@"Comments to analyze: {comments}";
    }
}