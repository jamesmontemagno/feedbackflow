using Azure;
using Azure.Data.Tables;

namespace SharedDump.Models;

/// <summary>
/// Represents a saved shared analysis stored in Azure Table Storage
/// PartitionKey: UserId (owner of the analysis)
/// RowKey: Analysis ID (the blob reference)
/// </summary>
public class SharedAnalysisEntity : ITableEntity
{
    /// <summary>
    /// Partition key - UserId (owner of the analysis)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Row key - Analysis ID (references the blob storage)
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the entity
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Entity tag for optimistic concurrency
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Analysis ID (same as RowKey, for convenience)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title of the analysis
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Summary of the analysis (limited version without full content)
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Source type of the analysis (e.g., "GitHub", "YouTube", "Reddit")
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// User input that generated this analysis (optional)
    /// </summary>
    public string? UserInput { get; set; }

    /// <summary>
    /// When this analysis was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the owner
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this analysis is publicly accessible
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// When this analysis was made public (if applicable)
    /// </summary>
    public DateTime? PublicSharedDate { get; set; }

    /// <summary>
    /// Default constructor for Table Storage
    /// </summary>
    public SharedAnalysisEntity() { }

    /// <summary>
    /// Constructor for creating a new shared analysis entry
    /// </summary>
    /// <param name="userId">User ID of the owner</param>
    /// <param name="analysisId">Unique analysis ID</param>
    /// <param name="analysisData">Analysis data to extract metadata from</param>
    /// <param name="isPublic">Whether the analysis should be public</param>
    public SharedAnalysisEntity(string userId, string analysisId, AnalysisData analysisData, bool isPublic = false)
    {
        PartitionKey = userId;
        RowKey = analysisId;
        Id = analysisId;
        UserId = userId;
        Title = analysisData.Title;
        Summary = analysisData.Summary;
        SourceType = analysisData.SourceType;
        UserInput = analysisData.UserInput;
        CreatedDate = analysisData.CreatedDate;
        IsPublic = isPublic;
        PublicSharedDate = isPublic ? DateTime.UtcNow : null;
    }
}
