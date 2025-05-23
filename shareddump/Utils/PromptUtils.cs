using System.Text;

namespace SharedDump.Utils;

/// <summary>
/// Provides utility methods for building and formatting AI prompts.
/// </summary>
/// <remarks>
/// This class centralizes prompt-related functionality to ensure consistent prompt building
/// and formatting across different services and components.
/// </remarks>
public static class PromptUtils
{
    /// <summary>
    /// Builds a standard analysis prompt for comment analysis.
    /// </summary>
    /// <param name="comments">The comments text to analyze.</param>
    /// <returns>A formatted prompt string for AI analysis.</returns>
    /// <example>
    /// <code>
    /// var prompt = PromptUtils.BuildAnalysisPrompt(commentsText);
    /// var messages = new[] { new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, prompt) };
    /// </code>
    /// </example>
    public static string BuildAnalysisPrompt(string comments)
    {
        return $@"Comments to analyze: {comments}";
    }

    /// <summary>
    /// Formats a custom prompt to ensure it ends with markdown formatting instruction.
    /// </summary>
    /// <param name="customPrompt">The custom prompt to format.</param>
    /// <returns>A formatted prompt with markdown instruction appended if needed.</returns>
    /// <example>
    /// <code>
    /// var formattedPrompt = PromptUtils.FormatCustomPrompt(userCustomPrompt);
    /// </code>
    /// </example>
    public static string FormatCustomPrompt(string? customPrompt)
    {
        if (string.IsNullOrEmpty(customPrompt))
        {
            return string.Empty;
        }

        var trimmedPrompt = customPrompt.TrimEnd();
        
        const string markdownInstruction = " Format your response in markdown.";
        if (!trimmedPrompt.EndsWith(markdownInstruction, StringComparison.OrdinalIgnoreCase))
        {
            trimmedPrompt += markdownInstruction;
        }
        
        return trimmedPrompt;
    }

    /// <summary>
    /// Combines multiple text fragments into a single prompt with proper formatting.
    /// </summary>
    /// <param name="sections">A dictionary of section titles and their content.</param>
    /// <returns>A formatted prompt combining all sections.</returns>
    /// <example>
    /// <code>
    /// var sections = new Dictionary&lt;string, string&gt;
    /// {
    ///     { "Title", threadTitle },
    ///     { "Content", threadContent },
    ///     { "Comments", commentsText }
    /// };
    /// var combinedPrompt = PromptUtils.CombineSections(sections);
    /// </code>
    /// </example>
    public static string CombineSections(Dictionary<string, string> sections)
    {
        var builder = new StringBuilder();
        
        foreach (var section in sections)
        {
            if (!string.IsNullOrEmpty(section.Value))
            {
                builder.AppendLine($"{section.Key}: {section.Value}");
                builder.AppendLine();
            }
        }
        
        return builder.ToString().TrimEnd();
    }
}