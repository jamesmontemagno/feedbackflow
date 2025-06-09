using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SharedDump.Models;

namespace SharedDump.Services.ExportStrategies;

/// <summary>
/// PDF export strategy implementation
/// </summary>
public class PdfExportStrategy : IExportStrategy
{
    public string FileExtension => ".pdf";
    public string MimeType => "application/pdf";

    public async Task<MemoryStream> ExportAsync(IEnumerable<AnalysisHistoryItem> items)
    {
        var memoryStream = new MemoryStream();
        
        try
        {
            // Configure QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
            
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text("Analysis Export")
                        .FontSize(20)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            column.Spacing(15);

                            // Export metadata
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"Export Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(10);
                                row.RelativeItem().AlignRight().Text($"Total Items: {items.Count()}").FontSize(10);
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            // Analysis items
                            foreach (var item in items)
                            {
                                column.Item().Column(itemColumn =>
                                {
                                    // Item header
                                    itemColumn.Item().Background(Colors.Grey.Lighten4)
                                        .Padding(10)
                                        .Column(headerCol =>
                                        {
                                            headerCol.Item().Text($"Analysis - {item.SourceType}")
                                                .FontSize(14)
                                                .Bold()
                                                .FontColor(Colors.Blue.Darken1);
                                            
                                            headerCol.Item().Text($"Date: {item.Timestamp:yyyy-MM-dd HH:mm:ss}")
                                                .FontSize(9)
                                                .FontColor(Colors.Grey.Darken1);
                                        });

                                    // Metadata section
                                    itemColumn.Item().Padding(10).Column(metaCol =>
                                    {
                                        metaCol.Item().Text("Metadata").FontSize(12).Bold();
                                        metaCol.Item().Text($"ID: {item.Id}").FontSize(9);
                                        
                                        if (!string.IsNullOrWhiteSpace(item.UserInput))
                                        {
                                            metaCol.Item().Text($"User Input: {item.UserInput}").FontSize(9);
                                        }
                                        
                                        metaCol.Item().Text($"Shared: {(item.IsShared ? "Yes" : "No")}").FontSize(9);
                                        
                                        if (item.IsShared && !string.IsNullOrWhiteSpace(item.SharedId))
                                        {
                                            metaCol.Item().Text($"Shared ID: {item.SharedId}").FontSize(9);
                                        }

                                        if (item.CommentThreads?.Any() == true)
                                        {
                                            metaCol.Item().Text($"Comment Threads: {item.CommentThreads.Count}").FontSize(9);
                                        }
                                    });

                                    // Summary section
                                    itemColumn.Item().Padding(10).Column(summaryCol =>
                                    {
                                        summaryCol.Item().Text("Summary").FontSize(12).Bold();
                                        summaryCol.Item().Text(item.Summary ?? "").FontSize(10);
                                    });

                                    // Full analysis section (if different from summary)
                                    if (!string.IsNullOrWhiteSpace(item.FullAnalysis) && 
                                        item.FullAnalysis != item.Summary)
                                    {
                                        itemColumn.Item().Padding(10).Column(analysisCol =>
                                        {
                                            analysisCol.Item().Text("Full Analysis").FontSize(12).Bold();
                                            analysisCol.Item().Text(item.FullAnalysis).FontSize(10);
                                        });
                                    }

                                    // Comment threads section
                                    if (item.CommentThreads?.Any() == true)
                                    {
                                        itemColumn.Item().Padding(10).Column(commentsCol =>
                                        {
                                            commentsCol.Item().Text("Comment Threads").FontSize(12).Bold();
                                            
                                            foreach (var thread in item.CommentThreads)
                                            {
                                                commentsCol.Item().PaddingVertical(5).Column(threadCol =>
                                                {
                                                    // Thread header
                                                    threadCol.Item().Background(Colors.Grey.Lighten5)
                                                        .Padding(5)
                                                        .Column(threadHeaderCol =>
                                                        {
                                                            threadHeaderCol.Item().Text(thread.Title)
                                                                .FontSize(11)
                                                                .Bold()
                                                                .FontColor(Colors.Blue.Darken2);
                                                            
                                                            threadHeaderCol.Item().Text($"Author: {thread.Author} | Created: {thread.CreatedAt:yyyy-MM-dd HH:mm:ss}")
                                                                .FontSize(8)
                                                                .FontColor(Colors.Grey.Darken1);

                                                            if (!string.IsNullOrEmpty(thread.Url))
                                                            {
                                                                threadHeaderCol.Item().Text($"URL: {thread.Url}")
                                                                    .FontSize(8)
                                                                    .FontColor(Colors.Blue.Medium);
                                                            }
                                                        });

                                                    // Thread description
                                                    if (!string.IsNullOrEmpty(thread.Description))
                                                    {
                                                        threadCol.Item().Padding(5).Text(thread.Description).FontSize(9);
                                                    }

                                                    // Comments summary (limit to avoid PDF size issues)
                                                    if (thread.Comments?.Any() == true)
                                                    {
                                                        var totalComments = CountCommentsRecursive(thread.Comments);
                                                        threadCol.Item().Padding(5).Text($"Total Comments: {totalComments}").FontSize(9).Italic();
                                                        
                                                        // Show only first few top-level comments in PDF to keep it manageable
                                                        var topComments = thread.Comments.Take(3);
                                                        foreach (var comment in topComments)
                                                        {
                                                            threadCol.Item().PaddingLeft(10).PaddingVertical(2).Column(commentCol =>
                                                            {
                                                                commentCol.Item().Text($"{comment.Author} - {comment.CreatedAt:yyyy-MM-dd}")
                                                                    .FontSize(8)
                                                                    .Bold();
                                                                
                                                                var truncatedContent = comment.Content.Length > 200 
                                                                    ? comment.Content.Substring(0, 200) + "..."
                                                                    : comment.Content;
                                                                
                                                                commentCol.Item().Text(truncatedContent).FontSize(8);
                                                                
                                                                if (comment.Replies?.Any() == true)
                                                                {
                                                                    commentCol.Item().Text($"({comment.Replies.Count} replies)")
                                                                        .FontSize(7)
                                                                        .Italic();
                                                                }
                                                            });
                                                        }
                                                        
                                                        if (thread.Comments.Count > 3)
                                                        {
                                                            threadCol.Item().PaddingLeft(10).Text($"... and {thread.Comments.Count - 3} more comments")
                                                                .FontSize(8)
                                                                .Italic();
                                                        }
                                                    }
                                                });
                                            }
                                        });
                                    }
                                });

                                column.Item().PaddingBottom(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf(memoryStream);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();
            throw;
        }
    }

    private static int CountCommentsRecursive(List<CommentData> comments)
    {
        int count = comments.Count;
        foreach (var comment in comments)
        {
            if (comment.Replies?.Any() == true)
            {
                count += CountCommentsRecursive(comment.Replies);
            }
        }
        return count;
    }
}