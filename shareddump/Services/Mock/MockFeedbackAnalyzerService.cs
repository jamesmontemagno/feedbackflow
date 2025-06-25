using Microsoft.Extensions.AI;
using SharedDump.AI;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of IFeedbackAnalyzerService for testing purposes
/// </summary>
public class MockFeedbackAnalyzerService : IFeedbackAnalyzerService
{
    private readonly Random _random = new(42); // Fixed seed for deterministic results

    public MockFeedbackAnalyzerService()
    {
        // MockFeedbackAnalyzerService now uses the shared MockAnalysisProvider
        // This ensures consistency across both the functions and webapp projects
    }

    public IChatClient CreateClient(string endpoint, string apiKey, string deploymentModel)
    {
        // For mock purposes, return null - this method won't be used in mock scenarios
        return null!;
    }

    public Task<string> AnalyzeCommentsAsync(string serviceType, string comments)
    {
        return AnalyzeCommentsAsync(serviceType, comments, null);
    }

    public Task<string> AnalyzeCommentsAsync(string serviceType, string comments, string? customSystemPrompt)
    {
        var analysisKey = serviceType.ToLowerInvariant();
        
        // Use the shared MockAnalysisProvider for consistent mock data
        var analysis = MockAnalysisProvider.GetMockAnalysis(analysisKey, EstimateCommentCount(comments), customSystemPrompt);

        // Simulate processing time
        return Task.Delay(100).ContinueWith(_ => analysis);
    }

    /// <summary>
    /// Estimates comment count from the comments string for analysis purposes
    /// </summary>
    private static int EstimateCommentCount(string comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
            return 0;
        
        // Simple heuristic: count line breaks and estimate comments
        var lines = comments.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return Math.Max(1, lines.Length / 3); // Assume ~3 lines per comment on average
    }

    public async IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments)
    {
        await foreach (var chunk in GetStreamingAnalysisAsync(serviceType, comments, null))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<string> GetStreamingAnalysisAsync(string serviceType, string comments, string? customSystemPrompt)
    {
        var analysis = await AnalyzeCommentsAsync(serviceType, comments, customSystemPrompt);
        var chunks = SplitIntoChunks(analysis, 50); // Split into ~50 character chunks

        foreach (var chunk in chunks)
        {
            await Task.Delay(50); // Simulate streaming delay
            yield return chunk;
        }
    }

    private static IEnumerable<string> SplitIntoChunks(string text, int chunkSize)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        for (int i = 0; i < text.Length; i += chunkSize)
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
        }
    }
}
