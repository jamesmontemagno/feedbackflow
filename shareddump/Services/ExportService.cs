using SharedDump.Models;
using SharedDump.Services.ExportStrategies;
using SharedDump.Services.Interfaces;

namespace SharedDump.Services;

/// <summary>
/// Implementation of the export service
/// </summary>
public class ExportService : IExportService
{
    private readonly Dictionary<ExportFormat, IExportStrategy> _strategies;

    public ExportService()
    {
        _strategies = new Dictionary<ExportFormat, IExportStrategy>
        {
            { ExportFormat.Csv, new CsvExportStrategy() },
            { ExportFormat.Json, new JsonExportStrategy() },
            { ExportFormat.Markdown, new MarkdownExportStrategy() },
            { ExportFormat.Pdf, new PdfExportStrategy() }
        };
    }

    public async Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items, ExportFormat format)
    {
        if (!_strategies.TryGetValue(format, out var strategy))
        {
            throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }

        return await strategy.ExportAsync(items);
    }

    public string GetFileExtension(ExportFormat format)
    {
        if (!_strategies.TryGetValue(format, out var strategy))
        {
            throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }

        return strategy.FileExtension;
    }

    public string GetMimeType(ExportFormat format)
    {
        if (!_strategies.TryGetValue(format, out var strategy))
        {
            throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }

        return strategy.MimeType;
    }
}