namespace SharedDump.Utils;

using System.Text.RegularExpressions;

/// <summary>
/// Utility methods for working with Markdown text
/// </summary>
public static class MarkdownUtils
{
    // Pre-compiled regex patterns for better performance
    private static readonly Regex HeaderPattern = new(@"#{1,6}\s+", RegexOptions.Compiled);
    private static readonly Regex BoldItalicAsteriskPattern = new(@"\*{1,3}", RegexOptions.Compiled);
    private static readonly Regex BoldItalicUnderscorePattern = new(@"_{1,3}", RegexOptions.Compiled);
    private static readonly Regex CodeBlockPattern = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\([^\)]+\)", RegexOptions.Compiled);
    private static readonly Regex ImagePattern = new(@"!\[([^\]]*)\]\([^\)]+\)", RegexOptions.Compiled);
    private static readonly Regex BulletListPattern = new(@"^\s*[-+*]\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex NumberedListPattern = new(@"^\s*\d+\.\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex BlockquotePattern = new(@"^\s*>\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HorizontalRulePattern = new(@"^\s*[-*_]{3,}\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex MultipleNewlinesPattern = new(@"\n\s*\n", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesPattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MultiplePeriodsPattern = new(@"\.{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Cleans markdown text for text-to-speech processing
    /// </summary>
    /// <param name="markdownText">The original markdown text</param>
    /// <returns>A cleaned plain text version suitable for speech synthesis</returns>
    public static string CleanForSpeech(string? markdownText)
    {
        if (string.IsNullOrEmpty(markdownText))
        {
            return string.Empty;
        }

        var text = markdownText;
        
        // Remove headers
        text = HeaderPattern.Replace(text, "");
        
        // Remove bold/italic markers
        text = BoldItalicAsteriskPattern.Replace(text, "");
        text = BoldItalicUnderscorePattern.Replace(text, "");
        
        // Remove code blocks and inline code
        text = CodeBlockPattern.Replace(text, "");
        text = text.Replace("`", "");
        
        // Remove links but keep text
        text = LinkPattern.Replace(text, "$1");
        
        // Remove images
        text = ImagePattern.Replace(text, "");
        
        // Clean up lists
        text = BulletListPattern.Replace(text, "• ");
        text = NumberedListPattern.Replace(text, "• ");
        
        // Remove blockquotes
        text = BlockquotePattern.Replace(text, "");
        
        // Remove horizontal rules
        text = HorizontalRulePattern.Replace(text, "");
        
        // Clean up whitespace
        text = MultipleNewlinesPattern.Replace(text, ". ");
        text = text.Replace("\n", ". ");
        
        // Remove multiple spaces
        text = MultipleSpacesPattern.Replace(text, " ");
        
        // Remove multiple periods
        text = MultiplePeriodsPattern.Replace(text, ".");
        
        return text.Trim();
    }
}
