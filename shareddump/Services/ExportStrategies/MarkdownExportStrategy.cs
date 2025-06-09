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

                if (item.CommentThreads?.Any() == true)
                {
                    await writer.WriteLineAsync($"- **Comment Threads:** {item.CommentThreads.Count}");
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

                // Add comment threads if they exist
                if (item.CommentThreads?.Any() == true)
                {
                    await WriteCommentThreads(writer, item.CommentThreads);
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

    private async Task WriteCommentThreads(StreamWriter writer, List<CommentThread> threads)
    {
        await writer.WriteLineAsync("### Comment Threads");
        await writer.WriteLineAsync();

        foreach (var thread in threads)
        {
            await writer.WriteLineAsync($"#### {thread.Title}");
            await writer.WriteLineAsync();
            
            await writer.WriteLineAsync($"- **ID:** {thread.Id}");
            await writer.WriteLineAsync($"- **Author:** {thread.Author}");
            await writer.WriteLineAsync($"- **Created:** {thread.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"- **Source:** {thread.SourceType}");
            
            if (!string.IsNullOrEmpty(thread.Url))
            {
                await writer.WriteLineAsync($"- **URL:** {thread.Url}");
            }

            if (!string.IsNullOrEmpty(thread.Description))
            {
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("**Description:**");
                await writer.WriteLineAsync(thread.Description);
            }

            if (thread.Comments?.Any() == true)
            {
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("**Comments:**");
                await writer.WriteLineAsync();
                await WriteCommentsRecursive(writer, thread.Comments, 0);
            }

            await writer.WriteLineAsync();
        }
    }

    private async Task WriteCommentsRecursive(StreamWriter writer, List<CommentData> comments, int depth)
    {
        var indent = new string(' ', depth * 2);
        
        foreach (var comment in comments)
        {
            await writer.WriteLineAsync($"{indent}- **{comment.Author}** _{comment.CreatedAt:yyyy-MM-dd HH:mm:ss}_");
            
            if (comment.Score.HasValue)
            {
                await writer.WriteLineAsync($"{indent}  Score: {comment.Score}");
            }
            
            // Split content into lines and indent each line
            var contentLines = comment.Content.Split('\n');
            foreach (var line in contentLines)
            {
                await writer.WriteLineAsync($"{indent}  {line}");
            }
            
            if (comment.Replies?.Any() == true)
            {
                await writer.WriteLineAsync();
                await WriteCommentsRecursive(writer, comment.Replies, depth + 1);
            }
            
            await writer.WriteLineAsync();
        }
    }
}