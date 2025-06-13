namespace SharedDump.Models;

public record AnalysisHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string FullAnalysis { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty; // e.g., "Manual", "Reddit", etc.
    public string? UserInput { get; init; } // Only set for manual mode
    public bool IsShared { get; init; } = false;
    public string? SharedId { get; init; } // ID for shared analysis reference
    public DateTime? SharedDate { get; init; } // Date when the analysis was shared
    
    /// <summary>
    /// Comment threads associated with this analysis (e.g., YouTube videos, Reddit threads, GitHub issues)
    /// Not stored in IndexedDB (filtered out in JS code) but used for export functionality
    /// </summary>
    public List<CommentThread> CommentThreads { get; init; } = new();
    
    /// <summary>
    /// Computed property that returns a truncated version of the full analysis for display in lists
    /// </summary>
    public string Summary => FullAnalysis.Length > 300 ? FullAnalysis.Substring(0, 300) + "..." : FullAnalysis;
}
