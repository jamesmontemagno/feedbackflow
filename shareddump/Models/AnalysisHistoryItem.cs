namespace SharedDump.Models;

public record AnalysisHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public string FullAnalysis { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty; // e.g., "Manual", "Reddit", etc.
    public string? UserInput { get; init; } // Only set for manual mode
    public bool IsShared { get; init; } = false;
    public string? SharedId { get; init; } // ID for shared analysis reference
    public DateTime? SharedDate { get; init; } // Date when the analysis was shared

    // Additional properties for sharing UI state
    public bool IsProcessingShare { get; init; } = false;
    public string ShareError { get; init; } = string.Empty;
}
