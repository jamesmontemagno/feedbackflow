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
            // Write analysis data
            await WriteAnalysisData(csv, items);
            
            // Write comment data if any exists
            await WriteCommentData(csv, items);

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

    private async Task WriteAnalysisData(CsvWriter csv, IEnumerable<AnalysisHistoryItem> items)
    {
        // Write analysis headers
        csv.WriteField("Id");
        csv.WriteField("Date");
        csv.WriteField("Source Type");
        csv.WriteField("User Input");
        csv.WriteField("Summary");
        csv.WriteField("Full Analysis");
        csv.WriteField("Is Shared");
        csv.WriteField("Shared Id");
        csv.WriteField("Shared Date");
        csv.WriteField("Comment Threads Count");
        await csv.NextRecordAsync();

        // Write analysis data
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
            csv.WriteField(item.CommentThreads?.Count ?? 0);
            await csv.NextRecordAsync();
        }
    }

    private async Task WriteCommentData(CsvWriter csv, IEnumerable<AnalysisHistoryItem> items)
    {
        var hasComments = items.Any(item => item.CommentThreads?.Any() == true);
        if (!hasComments) return;

        // Add empty line separator
        await csv.NextRecordAsync();

        // Write comment headers
        csv.WriteField("Analysis Id");
        csv.WriteField("Thread Id");
        csv.WriteField("Thread Title");
        csv.WriteField("Thread Author");
        csv.WriteField("Thread Created");
        csv.WriteField("Thread URL");
        csv.WriteField("Comment Id");
        csv.WriteField("Parent Comment Id");
        csv.WriteField("Comment Author");
        csv.WriteField("Comment Content");
        csv.WriteField("Comment Created");
        csv.WriteField("Comment Score");
        await csv.NextRecordAsync();

        // Write comment data
        foreach (var item in items)
        {
            if (item.CommentThreads == null) continue;

            foreach (var thread in item.CommentThreads)
            {
                // Write thread info with empty comment fields
                csv.WriteField(item.Id);
                csv.WriteField(thread.Id);
                csv.WriteField(thread.Title);
                csv.WriteField(thread.Author);
                csv.WriteField(thread.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                csv.WriteField(thread.Url ?? "");
                csv.WriteField(""); // Comment Id
                csv.WriteField(""); // Parent Comment Id
                csv.WriteField(""); // Comment Author
                csv.WriteField(""); // Comment Content
                csv.WriteField(""); // Comment Created
                csv.WriteField(""); // Comment Score
                await csv.NextRecordAsync();

                // Write all comments in this thread
                await WriteCommentsRecursive(csv, item.Id, thread.Id, thread.Comments);
            }
        }
    }

    private async Task WriteCommentsRecursive(CsvWriter csv, string analysisId, string threadId, List<CommentData> comments)
    {
        foreach (var comment in comments)
        {
            csv.WriteField(analysisId);
            csv.WriteField(threadId);
            csv.WriteField(""); // Thread Title
            csv.WriteField(""); // Thread Author
            csv.WriteField(""); // Thread Created
            csv.WriteField(""); // Thread URL
            csv.WriteField(comment.Id);
            csv.WriteField(comment.ParentId ?? "");
            csv.WriteField(comment.Author);
            csv.WriteField(comment.Content);
            csv.WriteField(comment.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.WriteField(comment.Score?.ToString() ?? "");
            await csv.NextRecordAsync();

            // Recursively write replies
            if (comment.Replies.Any())
            {
                await WriteCommentsRecursive(csv, analysisId, threadId, comment.Replies);
            }
        }
    }
}