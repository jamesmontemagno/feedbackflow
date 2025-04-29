using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace SharedDump.AI;

public class FeedbackAnalyzerService : IFeedbackAnalyzerService
{
    private readonly IChatClient _chatClient;

    public FeedbackAnalyzerService(string endpoint, string apiKey, string deploymentModel)
    {
        _chatClient = CreateClient(endpoint, apiKey, deploymentModel);
    }

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
        serviceType.ToLowerInvariant() switch
        {
            "youtube" => @"You are an expert at analyzing YouTube comments and like to use emoji to help bring visual spice to the analysis. 
                When analyzing comments, provide:
                1. A summary of the overall sentiment and viewer engagement
                2. Key topics, timestamps, or sections of the video mentioned frequently
                3. Most positive feedback and what viewers particularly liked
                4. Most constructive criticism or suggestions for improvement
                5. Common questions or points of confusion from viewers
                Format your response in markdown.",

            "github" => @"You are an expert at analyzing GitHub feedback and like to use emoji to help bring visual spice to the analysis. 
                When analyzing comments, provide:
                1. A summary of the overall sentiment and discussion quality
                2. Key technical topics, features, or issues mentioned
                3. Most insightful or helpful contributions
                4. Most critical issues or pain points raised
                5. Common feature requests or enhancement suggestions
                Format your response in markdown.",

            "hackernews" => @"📌 Meta Prompt for Hacker News Post Analysis
Given a Hacker News post along with all its comments, perform the following detailed analysis:
1. Overview and Key Themes
Provide a brief summary of what the post is about.
Identify the major themes emerging from the discussion.
Highlight points of strong consensus and disagreement among commenters.
2. Sentiment Analysis
Categorize comments by sentiment (Positive, Negative, Neutral).
For each sentiment category, select 3 representative comments to illustrate opinions clearly.
3. Feature and Topic Popularity
List the most frequently discussed features, technologies, frameworks, or companies mentioned in the comments.
Identify topics or keywords that generate high engagement (many replies or points).
4. Comparative Analysis
Summarize how commenters compare the primary technology or topic of the post to other competing technologies or concepts.
Identify and briefly describe the reasons behind positive or negative comparisons.
5. Trade-offs and Controversies
Highlight the most controversial topics or features from the discussion.
Clearly outline any important trade-offs identified by commenters.
6. Recommendations & Opportunities
Based on the discussion, suggest potential opportunities or improvements the primary subject could leverage.
Identify actionable insights or recommendations from the community feedback.
7. Final Summary
Provide a concise, actionable takeaway summarizing the community sentiment and actionable insights from the entire discussion.
Use this meta-prompt whenever you want a structured, insightful analysis of a Hacker News discussion.
                Format your response in markdown.",

            "reddit" => @"You are an expert at analyzing Reddit comments and like to use emoji to help bring visual spice to the analysis. 
                Given a Reddit thread along with all its comments, perform the following detailed analysis:
                1. Overview of the discussion and key themes
                2. Sentiment analysis of community response
                3. Most upvoted and insightful contributions
                4. Notable debates or disagreements
                5. Common questions and shared experiences
                Format your response in markdown.",

            "devblogs" => @"You are an expert at analyzing technical blog comments and like to use emoji to help bring visual spice to the analysis.
                Given a Microsoft DevBlogs article's comments, perform the following detailed analysis:
                1. Technical Discussion Quality
                   - Overall depth and technical accuracy of the discussions
                   - Key technical insights shared by commenters
                   - Any corrections or clarifications to the article content
                2. Sentiment Analysis
                   - Positive Technical Feedback
                     * Successfully implemented features/approaches
                     * Performance improvements noted
                     * Developer experience wins
                   - Constructive Technical Criticism
                     * Implementation challenges faced
                     * Performance concerns
                     * Breaking changes impact
                3. Knowledge Sharing
                   - Real-world experiences shared by developers
                   - Code examples or alternative approaches suggested
                   - Best practices or gotchas mentioned
                4. Community Engagement
                   - Questions asked and answers provided
                   - Areas where more documentation/examples were requested
                   - Cross-references to related technologies or articles
                5. Implementation Feedback
                   - Success stories and positive implementations
                   - Challenges or issues encountered
                   - Compatibility concerns or migration questions
                6. Future Requests
                   - Feature requests or enhancement suggestions
                   - Documentation improvements needed
                   - Areas where more guidance is desired
                Format your response in markdown with a focus on actionable technical insights.",

            "twitter" => @"You are an expert at analyzing Twitter/X conversations and like to use emoji to help bring visual spice to the analysis.
                When analyzing a tweet and its replies, provide:
                    1. A summary of the overall sentiment and engagement
                    2. Key topics, hashtags, or themes discussed
                    3. Most liked or insightful replies
                    4. Notable debates, disagreements, or controversies
                    5. Common questions, suggestions, or requests
                    Format your response in markdown.",
            "bluesky" => @"You are an expert at analyzing BlueSky conversations and like to use emoji to help bring visual spice to the analysis.
                When analyzing a BlueSky post and its replies, provide:
                1. A summary of the overall sentiment and engagement
                2. Key topics, hashtags, or themes discussed
                3. Most liked or insightful replies
                4. Notable debates, disagreements, or controversies
                5. Common questions, suggestions, or requests
                Format your response in markdown.",

            "manual" => @"You are an expert at analyzing content and providing structured insights. 
                When analyzing the provided text, please:
                1. Summarize the key points and themes
                2. Identify the sentiment and tone
                3. Extract any noteworthy information or insights
                4. Highlight questions, concerns, or areas needing clarification
                5. Provide actionable recommendations if applicable
                
                Format your response in markdown with clear sections and bullet points where appropriate.",

            _ => throw new ArgumentException($"Unknown service type: {serviceType}")
        };

    private static string BuildAnalysisPrompt(string comments)
    {
        return $@"Comments to analyze: {comments}";
    }
}