using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

public interface IAdminReportConfigService
{
    Task<List<AdminReportConfigModel>> GetAllConfigsAsync();
    Task<AdminReportConfigModel> CreateConfigAsync(AdminReportConfigModel config);
    Task<AdminReportConfigModel> UpdateConfigAsync(AdminReportConfigModel config);
    Task<bool> DeleteConfigAsync(string id);
    Task<bool> SendNowAsync(string id);
}