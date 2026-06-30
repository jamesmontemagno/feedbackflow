namespace SharedDump.Models.Reports;

/// <summary>
/// Lightweight metadata describing a generated report for the admin report-data portal.
/// </summary>
public class ReportDataListItem
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SubSource { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset CutoffDate { get; set; }
    public int ThreadCount { get; set; }
    public int CommentCount { get; set; }

    /// <summary>
    /// Whether downloadable raw data exists for this report.
    /// </summary>
    public bool HasRawData { get; set; }
}

/// <summary>
/// Response payload for the admin report listing endpoint.
/// </summary>
public class ReportDataListResponse
{
    public List<ReportDataListItem> Reports { get; set; } = new();
}
