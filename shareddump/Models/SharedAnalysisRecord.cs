namespace SharedDump.Models;

public record SharedAnalysisRecord
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime SharedDate { get; init; } = DateTime.UtcNow;
    public string SourceType { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
}