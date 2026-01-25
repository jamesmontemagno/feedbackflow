using System.Text;
using Microsoft.Extensions.AI;
using SharedDump.AI;

namespace SharedDump.Services.Mock;

/// <summary>
/// Mock implementation of IFeedbackAnalyzerService for testing purposes
/// </summary>
public class MockFeedbackAnalyzerService : IFeedbackAnalyzerService
{
    private const int MaxCommentsCharacterLength = 350_000;
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
        
        if (string.IsNullOrWhiteSpace(comments) || comments.Length <= MaxCommentsCharacterLength)
        {
            // Use the shared MockAnalysisProvider for consistent mock data
            var analysis = MockAnalysisProvider.GetMockAnalysis(analysisKey, EstimateCommentCount(comments), customSystemPrompt);
            return Task.Delay(100).ContinueWith(_ => analysis);
        }

        var chunks = SplitCommentsIntoChunks(comments);
        if (chunks.Count <= 1)
        {
            var analysis = MockAnalysisProvider.GetMockAnalysis(analysisKey, EstimateCommentCount(comments), customSystemPrompt);
            return Task.Delay(100).ContinueWith(_ => analysis);
        }

        var chunkAnalyses = chunks.Select(chunk =>
            MockAnalysisProvider.GetMockAnalysis(analysisKey, EstimateCommentCount(chunk), customSystemPrompt)).ToList();

        var combinePrompt = GetCombineSummariesPrompt();
        var combinedPrefix = $"{combinePrompt}\n\nCombined {chunkAnalyses.Count} chunk analyses.";
        var combinedAnalysis = MockAnalysisProvider.GetMockAnalysis(
            analysisKey,
            EstimateCommentCount(comments),
            combinedPrefix);

        // Simulate processing time
        return Task.Delay(100).ContinueWith(_ => combinedAnalysis);
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

    private static string GetCombineSummariesPrompt() =>
        "Combine the chunk analyses into a single cohesive summary.";

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
}
