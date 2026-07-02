using SharedDump.Models;

namespace FeedbackFunctions.Services.Sharing;

public interface ISharedAnalysisStorageService
{
    bool TryGetCachedAnalysis(string id, out string analysisJson);

    void CacheAnalysis(string id, string analysisJson);

    Task<string> SaveAnalysisAsync(string userId, AnalysisData analysisData, bool isPublic);

    void RemoveCachedAnalysis(string id);
}
