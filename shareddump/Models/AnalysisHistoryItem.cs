namespace SharedDump.Models;

public record AnalysisHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty; // e.g., "Manual", "Reddit", etc.
    public string? UserInput { get; init; } // Only set for manual mode
}
