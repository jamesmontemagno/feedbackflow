using System;

namespace SharedDump.Models.Reports;

public class ReportModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = string.Empty;
    public string SubSource { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string HtmlContent { get; set; } = string.Empty;
    public int ThreadCount { get; set; }
    public int CommentCount { get; set; }
    public DateTimeOffset CutoffDate { get; set; }
}
