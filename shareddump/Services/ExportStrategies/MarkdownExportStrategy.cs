using System.Text;
using SharedDump.Models;
using SharedDump.Utils;

namespace SharedDump.Services.ExportStrategies;

/// <summary>
/// Markdown export strategy implementation
/// </summary>
public class MarkdownExportStrategy : IExportStrategy
{
    public string FileExtension => ".md";
    public string MimeType => "text/markdown";

    public async Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items)
    {
        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream, Encoding.UTF8);

        try
        {
            await writer.WriteLineAsync("# Analysis Export");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"**Export Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            await writer.WriteLineAsync($"**Total Items:** {items.Count()}");
            await writer.WriteLineAsync();

            foreach (var item in items)
            {
                await writer.WriteLineAsync("---");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync($"## Analysis - {item.SourceType}");
                await writer.WriteLineAsync();
                
                await writer.WriteLineAsync("### Metadata");
                await writer.WriteLineAsync($"- **ID:** {item.Id}");
                await writer.WriteLineAsync($"- **Date:** {item.Timestamp:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"- **Source Type:** {item.SourceType}");
                
                if (!string.IsNullOrWhiteSpace(item.UserInput))
                {
                    await writer.WriteLineAsync($"- **User Input:** {item.UserInput}");
                }
                
                if (item.IsShared)
                {
                    await writer.WriteLineAsync($"- **Shared:** Yes (ID: {item.SharedId})");
                    if (item.SharedDate.HasValue)
                    {
                        await writer.WriteLineAsync($"- **Shared Date:** {item.SharedDate.Value:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                else
                {
                    await writer.WriteLineAsync("- **Shared:** No");
                }
                
                await writer.WriteLineAsync();
                
                await writer.WriteLineAsync("### Summary");
                await writer.WriteLineAsync(item.Summary);
                await writer.WriteLineAsync();
                
                if (!string.IsNullOrWhiteSpace(item.FullAnalysis) && item.FullAnalysis != item.Summary)
                {
                    await writer.WriteLineAsync("### Full Analysis");
                    await writer.WriteLineAsync(item.FullAnalysis);
                    await writer.WriteLineAsync();
                }
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