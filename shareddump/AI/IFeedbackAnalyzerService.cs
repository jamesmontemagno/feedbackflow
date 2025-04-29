using Microsoft.Extensions.AI;

namespace SharedDump.AI;

public interface IFeedbackAnalyzerService
{
    IChatClient CreateClient(string endpoint, string apiKey, string deploymentModel);
    Task<string> AnalyzeCommentsAsync(string serviceType, string comments);
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments);
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments, string? customSystemPrompt);
}