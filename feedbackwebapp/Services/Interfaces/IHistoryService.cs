
namespace FeedbackWebApp.Services.Interfaces;

using SharedDump.Models;

public interface IHistoryService
{
    Task<List<AnalysisHistoryItem>> GetHistoryAsync();
    Task SaveToHistoryAsync(AnalysisHistoryItem item);
    Task DeleteHistoryItemAsync(string id);
    Task ClearHistoryAsync();
}
