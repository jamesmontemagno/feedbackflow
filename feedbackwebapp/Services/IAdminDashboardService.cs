using SharedDump.Models.Admin;

namespace FeedbackWebApp.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardMetrics> GetDashboardMetricsAsync();
}