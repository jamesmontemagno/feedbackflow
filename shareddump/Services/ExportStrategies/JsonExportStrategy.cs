using System.Text;
using System.Text.Json;
using SharedDump.Models;

namespace SharedDump.Services.ExportStrategies;

/// <summary>
/// JSON export strategy implementation
/// </summary>
public class JsonExportStrategy : IExportStrategy
{
    public string FileExtension => ".json";
    public string MimeType => "application/json";

    public async Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items)
    {
        var memoryStream = new MemoryStream();
        
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await JsonSerializer.SerializeAsync(memoryStream, items, options);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();
            throw;
        }
    }
}