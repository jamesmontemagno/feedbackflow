using System.Globalization;
using System.Text;
using CsvHelper;
using SharedDump.Models;

namespace SharedDump.Services.ExportStrategies;

/// <summary>
/// CSV export strategy implementation
/// </summary>
public class CsvExportStrategy : IExportStrategy
{
    public string FileExtension => ".csv";
    public string MimeType => "text/csv";

    public async Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items)
    {
        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        try
        {
            // Write headers
            csv.WriteField("Id");
            csv.WriteField("Date");
            csv.WriteField("Source Type");
            csv.WriteField("User Input");
            csv.WriteField("Summary");
            csv.WriteField("Full Analysis");
            csv.WriteField("Is Shared");
            csv.WriteField("Shared Id");
            csv.WriteField("Shared Date");
            await csv.NextRecordAsync();

            // Write data
            foreach (var item in items)
            {
                csv.WriteField(item.Id);
                csv.WriteField(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                csv.WriteField(item.SourceType);
                csv.WriteField(item.UserInput ?? "");
                csv.WriteField(item.Summary);
                csv.WriteField(item.FullAnalysis);
                csv.WriteField(item.IsShared.ToString());
                csv.WriteField(item.SharedId ?? "");
                csv.WriteField(item.SharedDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                await csv.NextRecordAsync();
            }

            await writer.FlushAsync();
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await writer.DisposeAsync();
            await memoryStream.DisposeAsync();
            throw;
        }
    }
}