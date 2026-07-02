namespace FeedbackFunctions.FeedbackAnalysis;

public class DeveloperReportRequest
{
    public List<string> Urls { get; set; } = new();

    public string? CustomPrompt { get; set; }

    public bool SaveReport { get; set; }

    public bool IsPublic { get; set; } = true;
}

public class DeveloperReportSourceResult
{
    public string Url { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public bool HasComments { get; set; }
}

public class DeveloperReportResponse
{
    public string Analysis { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public List<string> Urls { get; set; } = new();

    public List<DeveloperReportSourceResult> Sources { get; set; } = new();

    public string? SavedAnalysisId { get; set; }

    public string? Url { get; set; }

    public bool IsPublic { get; set; }
}
