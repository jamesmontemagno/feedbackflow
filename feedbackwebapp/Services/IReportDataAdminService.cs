using SharedDump.Models.Reports;

namespace FeedbackWebApp.Services;

/// <summary>
/// Web application service for the admin report-data portal. Lists all generated reports
/// and downloads the raw fetched data for a given report from the Azure Functions backend.
/// </summary>
public interface IReportDataAdminService
{
    /// <summary>
    /// Gets metadata for all generated reports available in the system.
    /// </summary>
    Task<List<ReportDataListItem>> GetAllReportsAsync();

    /// <summary>
    /// Downloads the raw JSON data for the specified report. Returns null when no raw data exists.
    /// </summary>
    Task<string?> DownloadRawDataAsync(Guid reportId);
}
