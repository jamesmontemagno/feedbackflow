namespace SharedDump.Models;

public record AnalysisData
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FullAnalysis { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string? UserInput { get; init; }
    public DateTime CreatedDate { get; init; } = DateTime.UtcNow;
}