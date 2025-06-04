using Markdig;

namespace SharedDump.Utils;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>
    /// Converts Markdown text to HTML using advanced extensions
    /// </summary>
    /// <param name="markdown">The markdown text to convert</param>
    /// <returns>HTML string representation of the markdown</returns>
    public static string ConvertToHtml(string? markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, _markdownPipeline);
    }
}
