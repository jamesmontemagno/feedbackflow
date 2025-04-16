using System.Text;
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

    private static string GetServiceSpecificPrompt(string serviceType) =>
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

            "hackernews" => @"You are an expert at analyzing Hacker News comments and like to use emoji to help bring visual spice to the analysis. 
                When analyzing comments, provide:
                1. A summary of the overall discussion quality and technical depth
                2. Key technical insights and alternative perspectives shared
                3. Most insightful technical contributions or explanations
                4. Most critical technical concerns or limitations pointed out
                5. Interesting related technologies or approaches mentioned
                Format your response in markdown.",

            _ => throw new ArgumentException($"Invalid service type: {serviceType}", nameof(serviceType))
        };

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
        var servicePrompt = GetServiceSpecificPrompt(serviceType);
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

    private static string BuildAnalysisPrompt(string comments)
    {
        return $@"Comments to analyze: {comments}";
    }
}