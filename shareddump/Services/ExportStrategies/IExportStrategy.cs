using SharedDump.Models;

namespace SharedDump.Services.ExportStrategies;

/// <summary>
/// Interface for export format strategies
/// </summary>
public interface IExportStrategy
{
    /// <summary>
    /// Exports analysis history items to a memory stream
    /// </summary>
    /// <param name="items">The items to export</param>
    /// <returns>A memory stream containing the exported data</returns>
    Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items);
    
    /// <summary>
    /// Gets the file extension for this export format
    /// </summary>
    string FileExtension { get; }
    
    /// <summary>
    /// Gets the MIME type for this export format
    /// </summary>
    string MimeType { get; }
}