using SharedDump.Models.Reports;

namespace FeedbackFunctions.Services.Reports;

public interface IAdminReportConfigService
{
    Task<List<AdminReportConfigModel>> GetAllActiveConfigsAsync();
    Task<List<AdminReportConfigModel>> GetAllConfigsAsync();
    Task<AdminReportConfigModel?> GetConfigAsync(string id);
    Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config);
    Task<AdminReportConfigModel> UpdateConfigAsync(AdminReportConfigModel config);
    Task<bool> DeleteConfigAsync(string id);
    Task MarkConfigProcessedAsync(string id, DateTimeOffset processedAt);
}