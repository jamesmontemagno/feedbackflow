using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace SharedDump.AI;

/// <summary>
/// Types of analysis prompts available for feedback analysis
/// </summary>
public enum PromptType
{
    /// <summary>
    /// Product-focused feedback analysis (default) - analyzes feedback as if it's about your own product
    /// </summary>
    ProductFeedback,
    
    /// <summary>
    /// Competitor analysis - analyzes feedback about competitor products or services
    /// </summary>
    CompetitorAnalysis,
    
    /// <summary>
    /// General content analysis - neutral, objective analysis without product context
    /// </summary>
    GeneralAnalysis,
    
    /// <summary>
    /// Custom user-defined prompt
    /// </summary>
    Custom
}

/// <summary>
/// Service for analyzing feedback using AI models
/// </summary>
/// <remarks>
/// This service provides methods to analyze comments and feedback from various sources
/// using AI capabilities. It supports both synchronous and streaming analysis.
/// </remarks>
public class FeedbackAnalyzerService : IFeedbackAnalyzerService
{
    /// <summary>
    /// Maximum character length for comments sent to analysis.
    /// This prevents timeouts and rate limiting from the AI service.
    /// ~300K characters ≈ ~75K tokens for most content.
    /// </summary>
    private const int MaxCommentsCharacterLength = 350_000;

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
        if (string.IsNullOrWhiteSpace(comments) || comments.Length <= MaxCommentsCharacterLength)
        {
            return await AnalyzeWithPromptAsync(servicePrompt, comments, isCombineContent: false);
        }

        var chunks = SplitCommentsIntoChunks(comments);
        if (chunks.Count <= 1)
        {
            return await AnalyzeWithPromptAsync(servicePrompt, comments, isCombineContent: false);
        }

        var chunkAnalyses = new List<string>(chunks.Count);
        foreach (var chunk in chunks)
        {
            chunkAnalyses.Add(await AnalyzeWithPromptAsync(servicePrompt, chunk, isCombineContent: false));
        }

        var combinePrompt = GetCombineSummariesPrompt();
        var combinedInput = await PrepareCombinedInputAsync(combinePrompt, chunkAnalyses);
        return await AnalyzeWithPromptAsync(combinePrompt, combinedInput, isCombineContent: true);
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
        if (string.IsNullOrWhiteSpace(comments) || comments.Length <= MaxCommentsCharacterLength)
        {
            await foreach (var update in StreamWithPromptAsync(servicePrompt, comments, isCombineContent: false))
            {
                yield return update;
            }

            yield break;
        }

        var chunks = SplitCommentsIntoChunks(comments);
        if (chunks.Count <= 1)
        {
            await foreach (var update in StreamWithPromptAsync(servicePrompt, comments, isCombineContent: false))
            {
                yield return update;
            }

            yield break;
        }

        var chunkAnalyses = new List<string>(chunks.Count);
        foreach (var chunk in chunks)
        {
            chunkAnalyses.Add(await AnalyzeWithPromptAsync(servicePrompt, chunk, isCombineContent: false));
        }

        var combinePrompt = GetCombineSummariesPrompt();
        var combinedInput = await PrepareCombinedInputAsync(combinePrompt, chunkAnalyses);
        await foreach (var update in StreamWithPromptAsync(combinePrompt, combinedInput, isCombineContent: true))
        {
            yield return update;
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

    public static string GetUniversalPrompt() => GetPromptByType(PromptType.ProductFeedback);

    /// <summary>
    /// Gets the prompt template for the specified prompt type
    /// </summary>
    public static string GetPromptByType(PromptType promptType) => promptType switch
    {
        PromptType.ProductFeedback => GetProductFeedbackPrompt(),
        PromptType.CompetitorAnalysis => GetCompetitorAnalysisPrompt(),
        PromptType.GeneralAnalysis => GetGeneralAnalysisPrompt(),
        PromptType.Custom => GetProductFeedbackPrompt(), // Fallback to product feedback for custom
        _ => GetProductFeedbackPrompt()
    };

    private static string GetProductFeedbackPrompt() => @"# 🔍 Product Feedback Analysis Expert

You are an expert at analyzing customer feedback and user comments about products and services. You analyze feedback as if it's about YOUR product, providing actionable insights for improvement and growth.

When analyzing product feedback, provide this comprehensive breakdown:

# Analysis Title
- Provide a clear, descriptive title for the analysis
- Use emojis to enhance visual appeal and organization
- Include relevant context about the content source or type

## 🔑 TLDR
- Summarize the most critical findings and insights in 3-5 bullet points
- Highlight the most significant patterns, themes, or consensus points
- Identify the most actionable takeaways for the product team
- Note any urgent issues or opportunities that require immediate attention

## 📊 Feedback Context & Classification
- **Source Type**: Identify the platform, format, and content type (social media, technical discussion, video comments, blog feedback, etc.)
- **Audience & Intent**: Analyze the target audience and apparent purpose of the feedback
- **Quality & Engagement**: Assess overall discussion quality, engagement level, and participation patterns
- **Sentiment Distribution**: Break down positive, neutral, and negative sentiment with approximate percentages

## 🎯 Platform-Specific Insights
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
- **Core Themes**: List and analyze the primary topics and recurring themes in user feedback
- **Feature Requests**: Highlight the most requested features and improvements
- **Pain Points**: Identify specific problems and frustrations users are experiencing
- **Praise & Strengths**: Note what users appreciate and what's working well
- **Contrarian Views**: Note significant dissenting opinions or alternative perspectives
- **Unanswered Questions**: Compile important questions that remain unresolved

## 🔄 User Experience & Engagement
- **Interaction Patterns**: Analyze how users engage with your product and each other
- **Influence Networks**: Identify key contributors and their impact on the community
- **User Journey Issues**: Map pain points across the user journey
- **Quality Indicators**: Note what drives high-quality engagement and satisfaction

## 🌟 Strategic Recommendations
### Immediate Actions (Priority 1)
- List 2-3 urgent actions based on the most critical feedback
- Address any significant bugs, blockers, or user experience issues

### Product Improvements (Priority 2)
- Provide 3-5 actionable recommendations for product enhancements
- Suggest specific changes based on user feedback patterns
- Recommend areas for additional features, documentation, or examples

### Engagement & Community Building (Priority 3)
- Suggest strategies to enhance user engagement and satisfaction
- Recommend approaches for addressing common questions and concerns
- Identify opportunities for follow-up communication or engagement

### Long-term Product Strategy (Priority 4)
- Highlight trends that may impact future product development
- Suggest areas for innovation or expansion based on user needs
- Recommend focus areas for roadmap planning

## 📈 Success Metrics & Follow-up
- **Key Performance Indicators**: Suggest metrics to track the impact of recommended changes
- **Follow-up Topics**: Identify potential areas for deeper user research or additional analysis
- **Monitoring Recommendations**: Suggest what to watch for in future feedback cycles

Format your entire response using detailed markdown with clear section headers, bullet points, and occasional emojis for visual clarity. Adapt the depth and focus of each section based on the detected content type while maintaining comprehensive coverage. Prioritize actionable insights that help improve the product and user experience.";

    private static string GetCompetitorAnalysisPrompt() => @"# 🎯 Competitor Analysis Expert

You are an expert at analyzing feedback and discussions about competitor products and services. Your goal is to extract competitive intelligence, market positioning insights, and opportunities for differentiation.

When analyzing competitor-related content, provide this strategic breakdown:

# Analysis Title
- Provide a clear, descriptive title for the competitive analysis
- Use emojis to enhance visual appeal and organization
- Include relevant context about the competitor product or service

## 🔑 TLDR
- Summarize the most critical competitive insights in 3-5 bullet points
- Highlight key strengths and weaknesses of the competitor
- Identify opportunities for your product to differentiate or compete
- Note any significant market trends or shifts

## 📊 Competitor Context & Market Position
- **Source Type**: Identify where this feedback is coming from (social media, technical forums, review sites, etc.)
- **Target Audience**: Analyze who is using and discussing this competitor product
- **Market Positioning**: Assess how the competitor is positioned in the market
- **Sentiment Overview**: Break down positive, neutral, and negative sentiment with approximate percentages

## 💪 Competitor Strengths
- **What Users Praise**: Identify the most appreciated features and capabilities
- **Competitive Advantages**: Highlight areas where the competitor excels
- **User Satisfaction Drivers**: Note what keeps users loyal and satisfied
- **Successful Strategies**: Identify effective go-to-market or product strategies

## ⚠️ Competitor Weaknesses & Gaps
- **Common Complaints**: List frequent user frustrations and pain points
- **Missing Features**: Identify capabilities users wish the competitor had
- **User Experience Issues**: Note problems with usability, performance, or reliability
- **Support & Documentation Gaps**: Highlight areas where users struggle to get help
- **Pricing Concerns**: Note any feedback about cost, value, or pricing models

## 🎯 Differentiation Opportunities
- **Market Gaps**: Identify unmet needs that your product could address
- **Feature Opportunities**: Suggest features that could give you a competitive edge
- **User Experience Improvements**: Areas where you could deliver a superior experience
- **Target Audience Opportunities**: Underserved segments or use cases to focus on
- **Positioning Strategies**: Recommend messaging or positioning angles

## 📊 Competitive Intelligence
- **Pricing & Packaging**: Insights on competitor pricing and plan structure
- **Technology Stack**: Technical details about how the competitor solution works
- **Integration Ecosystem**: Partner integrations and ecosystem strategy
- **Go-to-Market Strategy**: How they acquire and retain customers
- **Recent Changes**: New features, updates, or strategic shifts

## 🔄 Market Dynamics & Trends
- **User Migration Patterns**: Note users switching to/from this competitor
- **Emerging Trends**: Industry trends reflected in the feedback
- **Buying Criteria**: What factors influence purchase decisions
- **Community Sentiment**: Overall market perception and brand strength

## 🌟 Strategic Recommendations
### Immediate Competitive Responses (Priority 1)
- Address competitor advantages that pose immediate threats
- Capitalize on competitor weaknesses with quick wins

### Product Differentiation (Priority 2)
- Develop features or capabilities that set you apart
- Improve areas where you can clearly beat the competition
- Build on your unique strengths

### Market Positioning (Priority 3)
- Refine your messaging to highlight competitive advantages
- Target underserved segments or use cases
- Build community and thought leadership

### Long-term Strategy (Priority 4)
- Plan for emerging market trends and shifts
- Develop sustainable competitive advantages
- Consider strategic partnerships or ecosystem plays

## 📈 Competitive Monitoring & Next Steps
- **Key Metrics to Track**: Suggest competitive intelligence metrics to monitor
- **Watch Areas**: Identify competitor aspects to keep monitoring
- **Research Opportunities**: Areas for deeper competitive research

Format your entire response using detailed markdown with clear section headers, bullet points, and occasional emojis for visual clarity. Focus on actionable competitive insights that inform product strategy, positioning, and differentiation.";

    private static string GetGeneralAnalysisPrompt() => @"# 📊 General Content Analysis Expert

You are an expert at objectively analyzing content, discussions, and feedback without specific product or competitive context. Provide balanced, neutral analysis focused on understanding the content, themes, and insights.

When analyzing content, provide this comprehensive breakdown:

# Analysis Title
- Provide a clear, descriptive title for the analysis
- Use emojis to enhance visual appeal and organization
- Include relevant context about the content source or type

## 🔑 TLDR
- Summarize the most important points in 3-5 bullet points
- Highlight key themes, patterns, or conclusions
- Provide essential takeaways for quick understanding
- Note any particularly significant or surprising insights

## 📊 Content Overview & Context
- **Source & Format**: Identify the platform, format, and content type
- **Topic & Purpose**: The main subject matter and intent of the content
- **Audience**: Who the content is aimed at and who's engaging with it
- **Scope & Depth**: Breadth and depth of the discussion
- **Engagement Level**: Overall participation and interaction patterns
- **Sentiment Distribution**: Breakdown of positive, neutral, and negative sentiment

## 🎯 Key Themes & Topics
- **Primary Themes**: The main topics and subjects discussed
- **Recurring Patterns**: Common threads across the content
- **Topic Evolution**: How the discussion develops and branches
- **Consensus Areas**: Topics where there's general agreement
- **Controversial Points**: Areas of debate or disagreement

## 💡 Notable Insights & Perspectives
- **Expert Contributions**: Particularly knowledgeable or insightful contributions
- **Unique Perspectives**: Novel or interesting viewpoints expressed
- **Data & Evidence**: Facts, statistics, or evidence shared
- **Examples & Case Studies**: Concrete examples or real-world applications
- **Questions Raised**: Important questions posed by participants
- **Knowledge Gaps**: Areas where information is missing or unclear

## 🗣️ Discussion Dynamics
- **Participation Patterns**: Who's contributing and how actively
- **Interaction Quality**: Depth and substance of exchanges
- **Conversation Flow**: How the discussion evolves over time
- **Influential Voices**: Contributors who shape the discussion
- **Community Norms**: Observable patterns in how people interact

## 📈 Content Quality & Value
- **Information Density**: How much substantive content is present
- **Accuracy & Credibility**: Reliability of information shared
- **Practical Value**: Actionable insights or useful information
- **Originality**: Novel ideas or perspectives versus rehashing
- **Completeness**: How thoroughly topics are covered

## 🌟 Key Takeaways & Observations
### Main Conclusions
- Core insights that emerge from the content
- Important patterns or trends identified

### Interesting Findings
- Unexpected or surprising observations
- Notable quotes or contributions

### Areas for Further Exploration
- Topics that warrant deeper investigation
- Questions that remain open

### Contextual Considerations
- Important context for interpreting the content
- Limitations or caveats to keep in mind

## 📋 Summary & Synthesis
- **Overall Assessment**: High-level view of the content value and quality
- **Key Patterns**: Most significant patterns or themes identified
- **Notable Gaps**: Important areas not covered or addressed
- **Relevance**: Who would find this content most valuable and why

Format your entire response using detailed markdown with clear section headers, bullet points, and occasional emojis for visual clarity. Maintain an objective, balanced perspective that accurately represents the content without bias toward product improvement or competitive positioning.";

    private static string BuildAnalysisPrompt(string comments)
    {
        return $@"Comments to analyze: {comments}";
    }

    private static string BuildCombinePrompt(string analyses)
    {
        return $@"Analyses to combine: {analyses}";
    }

    private static string GetCombineSummariesPrompt() =>
        @"You are combining multiple partial analyses into a single cohesive report. Synthesize the key themes, insights, sentiment, and recommendations across all chunks. Keep the same tone and markdown structure as the provided analyses. Do not reference chunk numbers or repeat content. Format your response in markdown.";

    private async Task<string> AnalyzeWithPromptAsync(string systemPrompt, string content, bool isCombineContent)
    {
        var prompt = isCombineContent ? BuildCombinePrompt(content) : BuildAnalysisPrompt(content);
        var messages = new[] { new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, prompt) };
        var response = await _chatClient.GetResponseAsync(messages);
        return response.ToString();
    }

    private async IAsyncEnumerable<string> StreamWithPromptAsync(string systemPrompt, string content, bool isCombineContent)
    {
        var prompt = isCombineContent ? BuildCombinePrompt(content) : BuildAnalysisPrompt(content);
        var messages = new[] { new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, prompt) };
        await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
        {
            yield return update.ToString();
        }
    }

    private static IReadOnlyList<string> SplitCommentsIntoChunks(string comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return Array.Empty<string>();
        }

        var chunks = new List<string>();
        var currentChunk = new StringBuilder();
        var lines = comments.Split('\n');

        foreach (var line in lines)
        {
            if (line.Length > MaxCommentsCharacterLength)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }

                foreach (var oversizedChunk in SplitOversizedLine(line))
                {
                    chunks.Add(oversizedChunk);
                }

                continue;
            }

            var separatorLength = currentChunk.Length > 0 ? 1 : 0;
            if (currentChunk.Length + separatorLength + line.Length > MaxCommentsCharacterLength)
            {
                chunks.Add(currentChunk.ToString());
                currentChunk.Clear();
            }

            if (currentChunk.Length > 0)
            {
                currentChunk.Append('\n');
            }

            currentChunk.Append(line);
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
    }

    private static IEnumerable<string> SplitOversizedLine(string line)
    {
        for (var index = 0; index < line.Length; index += MaxCommentsCharacterLength)
        {
            yield return line.Substring(index, Math.Min(MaxCommentsCharacterLength, line.Length - index));
        }
    }

    private static string BuildCombinedAnalysisContent(IReadOnlyList<string> analyses)
    {
        if (analyses.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < analyses.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }

            builder.AppendLine($"Chunk {index + 1} Analysis:");
            builder.AppendLine(analyses[index]);
        }

        return builder.ToString();
    }

    private async Task<string> PrepareCombinedInputAsync(string combinePrompt, IReadOnlyList<string> analyses)
    {
        var combinedContent = BuildCombinedAnalysisContent(analyses);
        if (combinedContent.Length <= MaxCommentsCharacterLength)
        {
            return combinedContent;
        }

        var condensedChunks = SplitCommentsIntoChunks(combinedContent);
        var condensedAnalyses = new List<string>(condensedChunks.Count);
        foreach (var chunk in condensedChunks)
        {
            condensedAnalyses.Add(await AnalyzeWithPromptAsync(combinePrompt, chunk, isCombineContent: true));
        }

        return await PrepareCombinedInputAsync(combinePrompt, condensedAnalyses);
    }
}
