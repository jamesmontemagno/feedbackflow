namespace FeedbackWebApp.Services.Interfaces;

using SharedDump.Models;

public interface IAnalysisSharingService
{
    Task<string> ShareAnalysisAsync(AnalysisData analysis);
    Task<AnalysisData?> GetSharedAnalysisAsync(string id);
    Task<List<SharedAnalysisRecord>> GetSharedAnalysisHistoryAsync();
    Task SaveSharedAnalysisToHistoryAsync(SharedAnalysisRecord record);
}