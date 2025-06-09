using SharedDump.Models;

namespace SharedDump.Services.Interfaces;

/// <summary>
/// Service for exporting analyzed comments to various formats
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports a collection of analysis history items to the specified format
    /// </summary>
    /// <param name="items">The analysis history items to export</param>
    /// <param name="format">The desired export format</param>
    /// <returns>A memory stream containing the exported data</returns>
    Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items, ExportFormat format);
    
    /// <summary>
    /// Gets the appropriate file extension for the given format
    /// </summary>
    /// <param name="format">The export format</param>
    /// <returns>The file extension including the dot (e.g., ".csv")</returns>
    string GetFileExtension(ExportFormat format);
    
    /// <summary>
    /// Gets the MIME type for the given format
    /// </summary>
    /// <param name="format">The export format</param>
    /// <returns>The MIME type</returns>
    string GetMimeType(ExportFormat format);
}